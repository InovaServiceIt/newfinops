using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Finops.Backend.Admin;

namespace Finops.Backend.Inventario;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record FiltroInventario(
    long ClienteId, long? ContaId, string? Regiao, string? Tipo, string? Compartimento, string? Estado,
    string? Busca, string? Tag, string? Sinal, bool Removidos);

public sealed record TagBusca(string? JsonDefined, string? JsonFreeform, string? Valor);

public sealed record ContagemItem(string Chave, long Total);
public sealed record CompartimentoOpcao(string Ocid, string Caminho);
public sealed record ProvenienciaConta(long ContaId, string? ContaNome, DateTimeOffset? VistoEm, long? Job);

public sealed record ResumoInventarioSaida(
    long TotalAtivos, long Novos7d, long Removidos7d,
    IReadOnlyList<ContagemItem> PorCategoria,
    IReadOnlyDictionary<string, long> Sinais,
    IReadOnlyList<string> Regioes, IReadOnlyList<ContagemItem> Tipos, IReadOnlyList<string> Estados,
    IReadOnlyList<CompartimentoOpcao> Compartimentos, IReadOnlyList<ProvenienciaConta> Proveniencia);

public sealed record RecursoItem(long ContaId, string Chave, string Nome, string Tipo, string Regiao, string? Compartimento,
    string? Estado, DateTimeOffset? CriadoEmCloud, JsonElement Tags, DateTimeOffset UltimoVistoEm, DateTimeOffset? RemovidoEm);

public sealed record PaginaRecursos(IReadOnlyList<RecursoItem> Itens, long Total, int Pagina, int Tamanho);

public sealed record RecursoDetalhe(long ContaId, string? ContaNome, string Chave, string Nome, string Tipo, string Regiao,
    string? CompartimentoOcid, string? Compartimento, string? Estado, DateTimeOffset? CriadoEmCloud, JsonElement Tags,
    JsonElement Atributos, DateTimeOffset PrimeiroVistoEm, DateTimeOffset UltimoVistoEm, DateTimeOffset? RemovidoEm, long? Job);

// ── Regras (puras) ───────────────────────────────────────────────────────────

public static class InventarioRegras
{
    public const int TamanhoPagina = 50;
    public const int LimiteCsv = 50_000;

    /// <summary>Sinais rápidos: chave → fragmento SQL CONSTANTE sobre o alias r (nunca entrada do usuário).</summary>
    public static readonly IReadOnlyDictionary<string, string> Sinais = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["volumes_soltos"] = "r.tipo IN ('block_storage.volumes', 'block_storage.boot_volumes') AND r.atributos @> '{\"anexado\": false}'",
        ["ips_sem_uso"] = "r.tipo = 'network.public_ips' AND r.atributos @> '{\"lifetime\": \"RESERVED\", \"atribuido\": false}'",
        ["instancias_paradas"] = "r.tipo = 'compute.instances' AND r.estado = 'STOPPED'",
        ["backups_antigos"] = "r.tipo IN ('block_storage.volume_backups', 'block_storage.boot_volume_backups') AND r.criado_em_cloud < now() - interval '90 days'",
        ["certificados_vencendo"] = "r.tipo = 'security.certificates' AND (CASE WHEN r.atributos->>'not_after' ~ '^\\d{4}-\\d{2}-\\d{2}' THEN CAST(left(r.atributos->>'not_after', 10) AS date) END) <= current_date + 30",
        ["sem_tag_defined"] = "COALESCE(r.tags->'defined', '{}'::jsonb) = '{}'::jsonb",
    };

    /// <summary>Ordenação: chave → expressão CONSTANTE.</summary>
    public static readonly IReadOnlyDictionary<string, string> Ordens = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["nome"] = "lower(r.nome)", ["tipo"] = "r.tipo", ["regiao"] = "r.regiao", ["compartimento"] = "c.caminho",
        ["estado"] = "r.estado", ["criado"] = "r.criado_em_cloud", ["visto"] = "r.ultimo_visto_em",
    };

    public static string? Limpo(string? s, int max = 300)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t.Length > max) throw ErroNegocioException.Validacao("Filtro muito longo.");
        return t;
    }

    public static FiltroInventario Validar(long? clienteId, long? contaId, string? regiao, string? tipo, string? compartimento,
        string? estado, string? busca, string? tag, string? sinal, bool removidos)
    {
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        if (contaId is <= 0) throw ErroNegocioException.Validacao("Conta inválida.");
        var s = Limpo(sinal, 40);
        if (s is not null && !Sinais.ContainsKey(s)) throw ErroNegocioException.Validacao("Sinal inválido.");
        return new FiltroInventario(clienteId.Value, contaId, Limpo(regiao, 60), Limpo(tipo, 60), Limpo(compartimento, 1000),
            Limpo(estado, 30), Limpo(busca, 300), Limpo(tag, 300), s, removidos);
    }

    public static (string Expressao, bool Desc) Ordem(string? ordem, string? direcao)
    {
        var o = Limpo(ordem, 20) ?? "nome";
        if (!Ordens.TryGetValue(o, out var expr)) throw ErroNegocioException.Validacao("Ordenação inválida.");
        var d = (Limpo(direcao, 4) ?? (o == "visto" ? "desc" : "asc")).ToLowerInvariant();
        if (d is not ("asc" or "desc")) throw ErroNegocioException.Validacao("Direção inválida.");
        return (expr, d == "desc");
    }

    /// <summary>
    /// "ns.chave=valor" → defined {ns:{chave:valor}} OU freeform {"ns.chave": valor};
    /// "chave=valor" → freeform {chave:valor}; só "valor" → qualquer valor de tag igual.
    /// </summary>
    public static TagBusca? Tag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var t = tag.Trim();
        var igual = t.IndexOf('=');
        if (igual < 0) return new TagBusca(null, null, t);

        var chave = t[..igual].Trim();
        var valor = t[(igual + 1)..].Trim();
        if (chave.Length == 0) return new TagBusca(null, null, valor);

        var freeform = new JsonObject { ["freeform"] = new JsonObject { [chave] = valor } }.ToJsonString();
        string? defined = null;
        var ponto = chave.IndexOf('.');
        if (ponto > 0 && ponto < chave.Length - 1)
            defined = new JsonObject { ["defined"] = new JsonObject { [chave[..ponto]] = new JsonObject { [chave[(ponto + 1)..]] = valor } } }.ToJsonString();
        return new TagBusca(defined, freeform, null);
    }

    /// <summary>Escapa curingas de LIKE (\ % _).</summary>
    public static string EscaparLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // ── CSV ──

    /// <summary>Célula segura: neutraliza fórmula (= + - @ tab CR no início) e aplica aspas.</summary>
    public static string CelulaCsv(string? valor)
    {
        var v = valor ?? "";
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            v = "'" + v;
        return v.IndexOfAny([';', '"', '\n', '\r', ',']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    public static readonly string[] CabecalhoCsv =
        ["conta", "nome", "chave", "tipo", "regiao", "compartimento", "estado", "criado_em_cloud", "ultimo_visto_em", "removido_em", "tags"];

    public static string LinhaCsv(IEnumerable<string?> celulas) => string.Join(';', celulas.Select(CelulaCsv));

    public static string Data(DateTimeOffset? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    /// <summary>UTF-8 com BOM (o Excel reconhece acentos) e ';' (padrão pt-BR).</summary>
    public static readonly Encoding CodificacaoCsv = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
}
