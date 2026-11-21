using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Finops.Backend.Admin;
using Finops.Backend.Inventario;

namespace Finops.Backend.Governanca;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record FiltroGovernanca(long ClienteId, long? ContaId, string? Regiao, string? Tipo, string? Compartimento, bool Automaticas);

public sealed record ValorContagem(string Valor, long N);

public sealed record ChaveTag(string Chave, string Origem, long Recursos, decimal CoberturaPct, long ValoresDistintos, IReadOnlyList<ValorContagem> TopValores);

public sealed record CoberturaTipo(string Tipo, long Total, long ComAlgumaTag);

public sealed record ResumoGovernanca(long TotalRecursos, long ComAlgumaTag, long SemTag, IReadOnlyList<ChaveTag> Chaves, bool Truncado,
    IReadOnlyList<CoberturaTipo> CoberturaPorTipo, IReadOnlyList<ProvenienciaConta> Proveniencia);

public sealed record PresencaChave(string Chave, long Presentes, long Ausentes);

public sealed record GrupoNaoConforme(string Nome, long Total, long NaoConformes);

public sealed record AvaliacaoGovernanca(long Total, long Conformes, long NaoConformes, decimal CoberturaPct,
    IReadOnlyList<PresencaChave> PorChave, IReadOnlyList<GrupoNaoConforme> PorCompartimento, IReadOnlyList<GrupoNaoConforme> PorTipo);

public sealed record Grafia(string Valor, long N);

public sealed record GrupoVariacao(string Normalizado, long Total, IReadOnlyList<Grafia> Grafias);

public sealed record NaoConformeItem(long ContaId, string Chave, string Nome, string Tipo, string Regiao, string? Compartimento,
    string? Estado, DateTimeOffset? CriadoEmCloud, JsonElement Tags, DateTimeOffset UltimoVistoEm, IReadOnlyList<string> Faltando);

public sealed record PaginaNaoConformes(IReadOnlyList<NaoConformeItem> Itens, long Total, int Pagina, int Tamanho);

// ── Regras (puras) ───────────────────────────────────────────────────────────

public static partial class GovernancaRegras
{
    public const int MaxChavesAvaliadas = 10;
    public const int MaxChavesResumo = 300;
    public const int TopValores = 5;
    public const int TopGrupos = 20;
    public const string Defined = "DEFINED";
    public const string Freeform = "FREEFORM";

    [GeneratedRegex(@"^[A-Za-z0-9_.\-:/ ]{1,200}$")]
    private static partial Regex ChaveValida();

    /// <summary>Tag automática da Oracle: namespace Oracle-Tags ou *-Oracle-Tags, chaves CreatedBy/CreatedOn.</summary>
    public static bool EhAutomatica(string ns, string chave) =>
        (ns == "Oracle-Tags" || ns.EndsWith("-Oracle-Tags", StringComparison.Ordinal)) && chave is "CreatedBy" or "CreatedOn";

    public static FiltroGovernanca Validar(long? clienteId, long? contaId, string? regiao, string? tipo, string? compartimento, bool automaticas)
    {
        var f = InventarioRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, null, null, null, null, false);
        return new FiltroGovernanca(f.ClienteId, f.ContaId, f.Regiao, f.Tipo, f.Compartimento, automaticas);
    }

    /// <summary>"ns.k1,k2" → lista distinta (1..10), cada uma validada pela regex.</summary>
    public static string[] Chaves(string? chaves, bool obrigatorio = true)
    {
        var lista = (chaves ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (lista.Length == 0)
        {
            if (obrigatorio) throw ErroNegocioException.Validacao("Selecione ao menos uma chave para avaliar.");
            return lista;
        }
        if (lista.Length > MaxChavesAvaliadas) throw ErroNegocioException.Validacao($"No máximo {MaxChavesAvaliadas} chaves por avaliação.");
        foreach (var c in lista)
            if (!ChaveValida().IsMatch(c)) throw ErroNegocioException.Validacao("Chave de tag inválida.");
        return lista;
    }

    public static string Chave(string? chave)
    {
        var c = (chave ?? "").Trim();
        if (!ChaveValida().IsMatch(c)) throw ErroNegocioException.Validacao("Chave de tag inválida.");
        return c;
    }

    public static decimal Pct(long parte, long total) => total == 0 ? 0m : Math.Round(parte * 100m / total, 1);

    /// <summary>Normaliza para comparar grafias: sem acento, minúsculo, espaços colapsados.</summary>
    public static string Normalizar(string valor)
    {
        var decomposto = valor.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var ch in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var semAcento = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return string.Join(' ', semAcento.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Grupos de valores que só diferem por caixa/espaço/acento (mais de uma grafia).</summary>
    public static List<GrupoVariacao> Variacoes(IEnumerable<Grafia> valores) =>
        valores.GroupBy(v => Normalizar(v.Valor))
            .Where(g => g.Count() > 1)
            .Select(g => new GrupoVariacao(g.Key, g.Sum(v => v.N),
                g.OrderByDescending(v => v.N).ThenBy(v => v.Valor, StringComparer.Ordinal).ToList()))
            .OrderByDescending(g => g.Total).ThenBy(g => g.Normalizado, StringComparer.Ordinal)
            .ToList();

    public static readonly string[] CabecalhoCsv =
        ["conta", "nome", "chave", "tipo", "regiao", "compartimento", "estado", "faltando", "ultimo_visto_em", "tags"];
}
