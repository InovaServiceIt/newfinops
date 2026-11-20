using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace CloudVista.Worker;

/// <summary>Uma linha de fato agregada (já somada por chave).</summary>
public sealed record LinhaCusto(DateOnly Periodo, string Chave, string Chave2, string Moeda, decimal Valor);

/// <summary>Série simples (previsão diária ou crédito mensal).</summary>
public sealed record LinhaSerie(DateOnly Periodo, string Moeda, decimal Valor);

/// <summary>
/// Bloco de custo_fato a substituir. Aplicar=false (truncado/falhou/ausente) → não apaga nada.
/// Intervalo de DELETE = menor e maior período RECEBIDOS. FiltroChave2 restringe o DELETE (tags).
/// </summary>
public sealed record BlocoFato(string Nome, string Dimensao, string Granularidade, string? FiltroChave2,
    IReadOnlyList<LinhaCusto> Linhas, bool Aplicar, string? Aviso)
{
    public DateOnly? De => Linhas.Count == 0 ? null : Linhas.Min(l => l.Periodo);
    public DateOnly? Ate => Linhas.Count == 0 ? null : Linhas.Max(l => l.Periodo);
}

public sealed record BlocoSerie(string Nome, IReadOnlyList<LinhaSerie> Linhas, bool Aplicar, string? Aviso)
{
    public DateOnly? De => Linhas.Count == 0 ? null : Linhas.Min(l => l.Periodo);
    public DateOnly? Ate => Linhas.Count == 0 ? null : Linhas.Max(l => l.Periodo);
}

public sealed record PlanoImportacao(IReadOnlyList<BlocoFato> Fatos, BlocoSerie Previsao, BlocoSerie Credito, IReadOnlyList<string> Avisos)
{
    /// <summary>resumo.importacao do job (só contagens, intervalo, moedas e avisos).</summary>
    public Dictionary<string, object?> Resumo()
    {
        var linhas = new Dictionary<string, int>();
        foreach (var b in Fatos)
        {
            var chave = $"{b.Dimensao.ToLowerInvariant()}_{(b.Granularidade == "MES" ? "mes" : "dia")}";
            linhas[chave] = linhas.GetValueOrDefault(chave) + (b.Aplicar ? b.Linhas.Count : 0);
        }
        linhas["previsao"] = Previsao.Aplicar ? Previsao.Linhas.Count : 0;
        linhas["credito"] = Credito.Aplicar ? Credito.Linhas.Count : 0;

        var servicoDia = Fatos.Where(b => b.Aplicar && b.Dimensao == "SERVICO" && b.Granularidade == "DIA").SelectMany(b => b.Linhas).ToList();
        var servicoMes = Fatos.Where(b => b.Aplicar && b.Dimensao == "SERVICO" && b.Granularidade == "MES").SelectMany(b => b.Linhas).ToList();
        var base_ = servicoDia.Count > 0 ? servicoDia : servicoMes;
        var periodo = base_.Count == 0 ? null : new Dictionary<string, string>
        {
            ["de"] = base_.Min(l => l.Periodo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["ate"] = base_.Max(l => l.Periodo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var moedas = Fatos.Where(b => b.Aplicar).SelectMany(b => b.Linhas.Select(l => l.Moeda))
            .Concat(Previsao.Linhas.Select(l => l.Moeda)).Concat(Credito.Linhas.Select(l => l.Moeda))
            .Distinct().Order(StringComparer.Ordinal).ToList();

        return new Dictionary<string, object?>
        {
            ["linhas"] = linhas,
            ["periodo_custo"] = periodo,
            ["moedas"] = moedas,
            ["avisos"] = Avisos,
        };
    }
}

/// <summary>Parser do bloco "cost" do coletor OCI do Maurício (funções puras, sem banco).</summary>
public static class ParserCustoOci
{
    public const int ChaveMax = 500;
    public const string MoedaDesconhecida = "XXX";

    /// <summary>(nome no JSON, dimensão, granularidade, campo do período, campo da chave, campo da chave2).</summary>
    private static readonly (string Nome, string Dim, string Gran, string Periodo, string Chave, string? Chave2)[] Blocos =
    [
        ("monthly_by_service", "SERVICO", "MES", "month", "service", null),
        ("daily_by_service", "SERVICO", "DIA", "day", "service", null),
        ("monthly_by_compartment", "COMPARTIMENTO", "MES", "month", "compartment_path", null),
        ("daily_by_compartment", "COMPARTIMENTO", "DIA", "day", "compartment_path", null),
        ("monthly_by_resource", "RECURSO", "MES", "month", "resource_id", "service"),
    ];

    public static PlanoImportacao Planejar(JsonElement cost)
    {
        var avisos = new List<string>();
        var consultas = LerConsultas(cost);
        var fatos = new List<BlocoFato>();

        foreach (var (nome, dim, gran, campoPeriodo, campoChave, campoChave2) in Blocos)
        {
            var (aplicar, aviso) = Situacao(cost, nome, nome, consultas);
            var linhas = aplicar
                ? Agregar(Itens(cost, nome), campoPeriodo, campoChave, it => campoChave2 is null ? "" : Texto(it, campoChave2) ?? "", avisos)
                : [];
            if (aviso is not null) avisos.Add(aviso);
            fatos.Add(new BlocoFato(nome, dim, gran, null, linhas, aplicar, aviso));
        }

        // Tags: uma consulta por tag (monthly_by_tag.{ns}.{key}); o DELETE é restrito à tag (chave2).
        var porTag = Itens(cost, "monthly_by_tag")
            .GroupBy(it => $"{Texto(it, "tag_namespace")}.{Texto(it, "tag_key")}")
            .ToDictionary(g => g.Key, g => g.ToList());
        var tagsConsultadas = consultas.Keys.Where(k => k.StartsWith("monthly_by_tag.", StringComparison.Ordinal))
            .Select(k => k["monthly_by_tag.".Length..]).Union(porTag.Keys).Distinct().Order(StringComparer.Ordinal);
        foreach (var tag in tagsConsultadas)
        {
            var (aplicar, aviso) = Situacao(cost, "monthly_by_tag", $"monthly_by_tag.{tag}", consultas);
            var linhas = aplicar
                ? Agregar(porTag.GetValueOrDefault(tag) ?? [], "month", "tag_value", _ => tag, avisos)
                : [];
            if (aviso is not null) avisos.Add(aviso);
            fatos.Add(new BlocoFato($"monthly_by_tag.{tag}", "TAG", "MES", Cortar(tag), linhas, aplicar, aviso));
        }

        // Previsão: só as linhas projetadas (is_forecast = true) de forecast.daily.
        var previsao = Serie(cost, "forecast", "daily", "day", "forecast_daily_total", consultas, avisos,
            it => it.TryGetProperty("is_forecast", out var f) && f.ValueKind == JsonValueKind.True);
        var credito = Serie(cost, "credits", "monthly", "month", "credits_monthly", consultas, avisos, _ => true);

        return new PlanoImportacao(fatos, previsao, credito, avisos.Distinct().ToList());
    }

    // ── Situação do bloco (query ok/truncated) ───────────────────────────────

    private static Dictionary<string, (bool Ok, bool Truncado)> LerConsultas(JsonElement cost)
    {
        var mapa = new Dictionary<string, (bool, bool)>(StringComparer.Ordinal);
        if (cost.TryGetProperty("queries", out var qs) && qs.ValueKind == JsonValueKind.Array)
        {
            // A última ocorrência vale (ex.: monthly_by_resource tenta 2 agrupamentos).
            foreach (var q in qs.EnumerateArray())
            {
                var nome = Texto(q, "name");
                if (nome is null) continue;
                var ok = !q.TryGetProperty("ok", out var o) || o.ValueKind != JsonValueKind.False;
                var trunc = q.TryGetProperty("truncated", out var t) && t.ValueKind == JsonValueKind.True;
                mapa[nome] = (ok, trunc);
            }
        }
        return mapa;
    }

    private static (bool Aplicar, string? Aviso) Situacao(JsonElement cost, string chaveJson, string nomeConsulta,
        IReadOnlyDictionary<string, (bool Ok, bool Truncado)> consultas)
    {
        if (!cost.TryGetProperty(chaveJson, out var bloco) || bloco.ValueKind == JsonValueKind.Null)
            return (false, $"{nomeConsulta}: bloco ausente (nada apagado)");
        if (consultas.TryGetValue(nomeConsulta, out var q))
        {
            if (!q.Ok) return (false, $"{nomeConsulta}: consulta falhou na coleta (nada apagado)");
            if (q.Truncado) return (false, $"{nomeConsulta}: consulta truncada (nada apagado)");
        }
        return (true, null);
    }

    // ── Linhas ───────────────────────────────────────────────────────────────

    private static List<JsonElement> Itens(JsonElement cost, string nome) =>
        cost.TryGetProperty(nome, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().ToList() : [];

    private static List<LinhaCusto> Agregar(IEnumerable<JsonElement> itens, string campoPeriodo, string campoChave,
        Func<JsonElement, string> chave2, List<string> avisos)
    {
        var soma = new Dictionary<(DateOnly, string, string, string), decimal>();
        foreach (var it in itens)
        {
            if (!TentarPeriodo(Texto(it, campoPeriodo), out var periodo) || !TentarValor(it, out var valor))
                continue;
            var chave = Cortar(Texto(it, campoChave) is { Length: > 0 } c ? c : "(sem valor)");
            var moeda = Moeda(it, avisos);
            var k = (periodo, chave, Cortar(chave2(it)), moeda);
            soma[k] = soma.GetValueOrDefault(k) + valor;   // chave repetida no mesmo período: soma
        }
        return soma.Select(kv => new LinhaCusto(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4, kv.Value))
            .OrderBy(l => l.Periodo).ThenBy(l => l.Chave, StringComparer.Ordinal).ThenBy(l => l.Chave2, StringComparer.Ordinal).ToList();
    }

    private static BlocoSerie Serie(JsonElement cost, string objeto, string lista, string campoPeriodo, string nomeConsulta,
        IReadOnlyDictionary<string, (bool Ok, bool Truncado)> consultas, List<string> avisos, Func<JsonElement, bool> incluir)
    {
        var (aplicar, aviso) = Situacao(cost, objeto, nomeConsulta, consultas);
        if (aviso is not null) avisos.Add(aviso);
        var linhas = new List<LinhaSerie>();
        if (aplicar && cost.GetProperty(objeto).ValueKind == JsonValueKind.Object
            && cost.GetProperty(objeto).TryGetProperty(lista, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var soma = new Dictionary<(DateOnly, string), decimal>();
            foreach (var it in arr.EnumerateArray())
            {
                if (!incluir(it) || !TentarPeriodo(Texto(it, campoPeriodo), out var p) || !TentarValor(it, out var v)) continue;
                var k = (p, Moeda(it, avisos));
                soma[k] = soma.GetValueOrDefault(k) + v;
            }
            linhas = soma.Select(kv => new LinhaSerie(kv.Key.Item1, kv.Key.Item2, kv.Value)).OrderBy(l => l.Periodo).ToList();
        }
        return new BlocoSerie(nomeConsulta, linhas, aplicar, aviso);
    }

    private static bool TentarPeriodo(string? s, out DateOnly periodo)
    {
        periodo = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length == 7) s += "-01";               // "YYYY-MM" → 1º dia do mês
        return DateOnly.TryParseExact(s.Length >= 10 ? s[..10] : s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out periodo);
    }

    private static bool TentarValor(JsonElement it, out decimal valor)
    {
        valor = 0;
        return it.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetDecimal(out valor);
    }

    private static string Moeda(JsonElement it, List<string> avisos)
    {
        var m = Texto(it, "currency")?.Trim().ToUpperInvariant();
        if (m is { Length: 3 }) return m;
        avisos.Add("Linha(s) sem moeda válida importada(s) como XXX");
        return MoedaDesconhecida;
    }

    private static string? Texto(JsonElement e, string campo) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Cortar(string s) => s.Length <= ChaveMax ? s : s[..ChaveMax];
}

/// <summary>Leitura da coleta_bruta (funções puras).</summary>
public static class ColetaBruta
{
    public static bool Sha256Confere(byte[] conteudo, string? sha256Esperado) =>
        !string.IsNullOrWhiteSpace(sha256Esperado)
        && string.Equals(Convert.ToHexString(SHA256.HashData(conteudo)), sha256Esperado.Trim(), StringComparison.OrdinalIgnoreCase);

    public static byte[] Descomprimir(byte[] gzip)
    {
        using var entrada = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress);
        using var saida = new MemoryStream();
        entrada.CopyTo(saida);
        return saida.ToArray();
    }
}
