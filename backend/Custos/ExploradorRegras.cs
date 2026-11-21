using System.Globalization;
using System.Text.RegularExpressions;
using Finops.Backend.Admin;
using Finops.Backend.Inventario;

namespace Finops.Backend.Custos;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ConsultaExplorador(long ClienteId, long? ContaId, string Granularidade, DateOnly De, DateOnly Ate,
    string Dimensao, IReadOnlyList<string> FiltroChave, string? TagChave, string? Moeda, bool ComFator, int Top)
{
    /// <summary>Período anterior de mesmo tamanho (DIA: mesmo nº de dias; MES: mesmo nº de meses).</summary>
    public (DateOnly De, DateOnly Ate) Anterior => ExploradorRegras.PeriodoAnterior(Granularidade, De, Ate);
}

public sealed record GrupoValor(string Chave, decimal Valor);
public sealed record PontoSerie(string Periodo, IReadOnlyList<GrupoValor> Grupos, decimal Outros);
public sealed record LinhaTabela(string Chave, string Rotulo, decimal Total, decimal Pct, decimal? VariacaoPct,
    IReadOnlyList<decimal> Serie, long? ContaId);
public sealed record ProvenienciaCusto(long ContaId, string? ContaNome, DateTimeOffset? AtualizadoEm, long? Job);

public sealed record RespostaExplorador(
    string Granularidade, string Dimensao, string De, string Ate, string? Moeda, IReadOnlyList<string> Moedas,
    bool ComFator, bool FatorDisponivel, IReadOnlyList<string> Grupos, IReadOnlyList<PontoSerie> Serie,
    IReadOnlyList<LinhaTabela> Tabela, bool TabelaTruncada, decimal Total, decimal? TotalPeriodoAnterior, decimal? Creditos,
    IReadOnlyList<ProvenienciaCusto> Proveniencia, bool Vazio);

/// <summary>Rótulo de chave vindo do inventário (RECURSO: nome; COMPARTIMENTO: caminho) e conta para abrir o painel.</summary>
public sealed record RotuloChave(string Rotulo, long? ContaId);

// ── Regras (puras) ───────────────────────────────────────────────────────────

public static partial class ExploradorRegras
{
    public static readonly string[] Granularidades = ["DIA", "MES"];
    public static readonly string[] Dimensoes = ["SERVICO", "COMPARTIMENTO", "RECURSO", "TAG"];
    public const int MaxDias = 366, MaxMeses = 36, MaxFiltro = 20, MaxLinhasTabela = 200;
    public const string Outros = "Outros";

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex MoedaValida();

    public static ConsultaExplorador Validar(long? clienteId, long? contaId, string? granularidade, DateOnly? de, DateOnly? ate,
        string? dimensao, string? filtroChave, string? tagChave, string? moeda, bool comFator, int? top)
    {
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        if (contaId is <= 0) throw ErroNegocioException.Validacao("Conta inválida.");

        var g = (granularidade ?? "MES").Trim().ToUpperInvariant();
        if (!Granularidades.Contains(g)) throw ErroNegocioException.Validacao("Granularidade: use DIA ou MES.");
        var d = (dimensao ?? "SERVICO").Trim().ToUpperInvariant();
        if (!Dimensoes.Contains(d)) throw ErroNegocioException.Validacao("Agrupar por: use SERVICO, COMPARTIMENTO, RECURSO ou TAG.");
        if (de is null || ate is null) throw ErroNegocioException.Validacao("Período: informe início e fim.");

        var (ini, fim) = g == "MES" ? (PrimeiroDoMes(de.Value), PrimeiroDoMes(ate.Value)) : (de.Value, ate.Value);
        if (ini > fim) throw ErroNegocioException.Validacao("Período: a data inicial deve ser anterior ou igual à final.");
        if (g == "DIA" && fim.DayNumber - ini.DayNumber + 1 > MaxDias) throw ErroNegocioException.Validacao($"Período diário: no máximo {MaxDias} dias.");
        if (g == "MES" && Meses(ini, fim) > MaxMeses) throw ErroNegocioException.Validacao($"Período mensal: no máximo {MaxMeses} meses.");

        var tag = InventarioRegras.Limpo(tagChave, 500);
        if (d == "TAG" && tag is null) throw ErroNegocioException.Validacao("Agrupar por tag: escolha a chave da tag.");
        if (d == "TAG" && g == "DIA") throw ErroNegocioException.Validacao("Custo por tag só existe por mês: use a granularidade MES.");

        var filtro = (filtroChave ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToList();
        if (filtro.Count > MaxFiltro) throw ErroNegocioException.Validacao($"Filtro: no máximo {MaxFiltro} valores.");
        if (filtro.Any(f => f.Length > 500)) throw ErroNegocioException.Validacao("Filtro: valor muito longo.");

        var m = InventarioRegras.Limpo(moeda, 3)?.ToUpperInvariant();
        if (m is not null && !MoedaValida().IsMatch(m)) throw ErroNegocioException.Validacao("Moeda inválida.");

        var t = top ?? 10;
        if (t is < 5 or > 50) throw ErroNegocioException.Validacao("Top: entre 5 e 50.");

        return new ConsultaExplorador(clienteId.Value, contaId, g, ini, fim, d, filtro, d == "TAG" ? tag : null, m, comFator, t);
    }

    public static DateOnly PrimeiroDoMes(DateOnly d) => new(d.Year, d.Month, 1);
    public static int Meses(DateOnly de, DateOnly ate) => (ate.Year - de.Year) * 12 + ate.Month - de.Month + 1;

    public static (DateOnly De, DateOnly Ate) PeriodoAnterior(string granularidade, DateOnly de, DateOnly ate)
    {
        if (granularidade == "MES")
        {
            var n = Meses(de, ate);
            return (de.AddMonths(-n), de.AddMonths(-1));
        }
        var dias = ate.DayNumber - de.DayNumber + 1;
        return (de.AddDays(-dias), de.AddDays(-1));
    }

    /// <summary>Todos os períodos do intervalo (preenche com zero onde não houve custo).</summary>
    public static List<DateOnly> Periodos(string granularidade, DateOnly de, DateOnly ate)
    {
        var lista = new List<DateOnly>();
        for (var p = de; p <= ate; p = granularidade == "MES" ? p.AddMonths(1) : p.AddDays(1)) lista.Add(p);
        return lista;
    }

    public static string Rotulo(string granularidade, DateOnly p) =>
        p.ToString(granularidade == "MES" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Moeda da consulta: a pedida (se existir no período) ou a de maior valor.</summary>
    public static string? EscolherMoeda(IEnumerable<LinhaAgregada> atuais, string? pedida)
    {
        var totais = atuais.GroupBy(l => l.Moeda).Select(g => (g.Key, Total: g.Sum(l => l.Valor))).ToList();
        if (pedida is not null && totais.Any(t => t.Key == pedida)) return pedida;
        return totais.OrderByDescending(t => t.Total).ThenBy(t => t.Key, StringComparer.Ordinal).Select(t => (string?)t.Key).FirstOrDefault();
    }

    public static decimal? Variacao(decimal atual, decimal? anterior) =>
        anterior is null or 0m ? null : Math.Round((atual - anterior.Value) * 100m / Math.Abs(anterior.Value), 1);

    /// <summary>
    /// Monta série (top N + Outros, somando exatamente o total) e tabela. As linhas já vêm agregadas
    /// do banco (GROUP BY período, chave, moeda) e, se for o caso, com o fator aplicado.
    /// </summary>
    public static RespostaExplorador Montar(ConsultaExplorador c, IReadOnlyList<LinhaAgregada> linhas, decimal? creditos,
        IReadOnlyDictionary<string, RotuloChave> rotulos, bool comFator, bool fatorDisponivel, IReadOnlyList<ProvenienciaCusto> proveniencia)
    {
        var (antDe, antAte) = c.Anterior;
        var atuais = linhas.Where(l => l.Periodo >= c.De && l.Periodo <= c.Ate).ToList();
        var moedas = atuais.Select(l => l.Moeda).Distinct().Order(StringComparer.Ordinal).ToList();
        var moeda = EscolherMoeda(atuais, c.Moeda);

        var doMoeda = atuais.Where(l => l.Moeda == moeda).ToList();
        var anteriores = linhas.Where(l => l.Moeda == moeda && l.Periodo >= antDe && l.Periodo <= antAte).ToList();

        var porChave = doMoeda.GroupBy(l => l.Chave).Select(g => (Chave: g.Key, Total: g.Sum(l => l.Valor)))
            .OrderByDescending(x => x.Total).ThenBy(x => x.Chave, StringComparer.Ordinal).ToList();
        var top = porChave.Take(c.Top).Select(x => x.Chave).ToList();
        var topSet = top.ToHashSet(StringComparer.Ordinal);

        var periodos = Periodos(c.Granularidade, c.De, c.Ate);
        var serie = periodos.Select(p =>
        {
            var doPeriodo = doMoeda.Where(l => l.Periodo == p).ToList();
            var grupos = top.Select(k => new GrupoValor(k, doPeriodo.Where(l => l.Chave == k).Sum(l => l.Valor))).ToList();
            return new PontoSerie(Rotulo(c.Granularidade, p), grupos, doPeriodo.Where(l => !topSet.Contains(l.Chave)).Sum(l => l.Valor));
        }).ToList();

        var total = doMoeda.Sum(l => l.Valor);
        decimal? totalAnterior = anteriores.Count == 0 ? null : anteriores.Sum(l => l.Valor);
        var anteriorPorChave = anteriores.GroupBy(l => l.Chave).ToDictionary(g => g.Key, g => g.Sum(l => l.Valor), StringComparer.Ordinal);
        var serieChave = doMoeda.GroupBy(l => (l.Chave, l.Periodo)).ToDictionary(g => g.Key, g => g.Sum(l => l.Valor));

        var tabela = porChave.Take(MaxLinhasTabela).Select(x =>
        {
            var rot = rotulos.GetValueOrDefault(x.Chave);
            return new LinhaTabela(x.Chave, rot?.Rotulo ?? x.Chave, x.Total, total == 0 ? 0 : Math.Round(x.Total * 100m / total, 1),
                Variacao(x.Total, anteriorPorChave.TryGetValue(x.Chave, out var a) ? a : null),
                periodos.Select(p => serieChave.GetValueOrDefault((x.Chave, p))).ToList(), rot?.ContaId);
        }).ToList();

        return new RespostaExplorador(c.Granularidade, c.Dimensao, Rotulo(c.Granularidade, c.De), Rotulo(c.Granularidade, c.Ate),
            moeda, moedas, comFator, fatorDisponivel, top, serie, tabela, porChave.Count > MaxLinhasTabela, total, totalAnterior,
            c.Granularidade == "MES" ? creditos : null, proveniencia, doMoeda.Count == 0);
    }

    public static readonly string[] CabecalhoCsv = ["periodo", "chave", "rotulo", "valor", "moeda", "fator_aplicado"];
}
