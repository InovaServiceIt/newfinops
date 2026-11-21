using System.Globalization;
using Finops.Backend.Admin;

namespace Finops.Backend.Custos;

// ── Fator do cliente ─────────────────────────────────────────────────────────

public sealed record FatorCliente(long Id, long ClienteId, string? ClienteNome, long? CloudContaId, string? ContaNome,
    decimal Fator, DateOnly VigenteDesde, DateOnly? VigenteAte, DateTimeOffset CriadoEm, string? CriadoPor);

public sealed record FatorEntrada(long? ClienteId, long? CloudContaId, decimal? Fator, DateOnly? VigenteDesde, DateOnly? VigenteAte);

public static class FatorRegras
{
    public const decimal FatorMax = 9999.999999m; // numeric(10,6)

    public static (long ClienteId, long? ContaId, decimal Fator, DateOnly Desde, DateOnly? Ate) Validar(FatorEntrada e)
    {
        if (e.ClienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        if (e.Fator is not { } f || f <= 0) throw ErroNegocioException.Validacao("Fator: informe um número maior que zero.");
        if (f > FatorMax) throw ErroNegocioException.Validacao($"Fator: no máximo {FatorMax.ToString(CultureInfo.InvariantCulture)}.");
        if (decimal.Round(f, 6) != f) throw ErroNegocioException.Validacao("Fator: no máximo 6 casas decimais.");
        if (e.VigenteDesde is not { } desde) throw ErroNegocioException.Validacao("Vigência: informe a data de início.");
        if (e.VigenteAte is { } ate && ate < desde) throw ErroNegocioException.Validacao("Vigência: a data final deve ser igual ou posterior à inicial.");
        return (e.ClienteId.Value, e.CloudContaId, f, desde, e.VigenteAte);
    }

    /// <summary>Duas vigências do mesmo cliente + mesma conta (ou ambas do cliente todo) não podem se sobrepor.</summary>
    public static bool Sobrepoe(IEnumerable<FatorCliente> existentes, long? ignorarId, long? contaId, DateOnly desde, DateOnly? ate) =>
        existentes.Any(f => f.Id != ignorarId && f.CloudContaId == contaId
            && f.VigenteDesde <= (ate ?? DateOnly.MaxValue) && desde <= (f.VigenteAte ?? DateOnly.MaxValue));

    /// <summary>
    /// Regra única (Visão Geral e Explorador): multiplica cada linha pelo fator vigente na data do
    /// período; período sem fator vigente fica com o valor bruto.
    /// </summary>
    public static IReadOnlyList<LinhaAgregada> Aplicar(IReadOnlyList<LinhaAgregada> linhas, IReadOnlyList<FatorCliente> fatores, long? contaId) =>
        fatores.Count == 0 ? linhas : linhas.Select(l => l with { Valor = l.Valor * (Vigente(fatores, contaId, l.Periodo) ?? 1m) }).ToList();

    /// <summary>Há fator vigente hoje ou em algum dos períodos considerados?</summary>
    public static bool Disponivel(IReadOnlyList<FatorCliente> fatores, long? contaId, DateOnly hoje, IEnumerable<DateOnly> periodos) =>
        fatores.Count > 0 && (Vigente(fatores, contaId, hoje) is not null || periodos.Distinct().Any(p => Vigente(fatores, contaId, p) is not null));

    /// <summary>
    /// Fator vigente numa data. Com conta: prefere o da conta, senão o do cliente (conta nula).
    /// Sem conta (visão do cliente todo): só o do cliente. Empate: o de vigência mais recente.
    /// </summary>
    public static decimal? Vigente(IEnumerable<FatorCliente> fatores, long? contaId, DateOnly data)
    {
        var vigentes = fatores.Where(f => f.VigenteDesde <= data && (f.VigenteAte is null || f.VigenteAte >= data)).ToList();
        FatorCliente? Mais(IEnumerable<FatorCliente> c) => c.OrderByDescending(f => f.VigenteDesde).ThenByDescending(f => f.Id).FirstOrDefault();
        var escolhido = (contaId is { } conta ? Mais(vigentes.Where(f => f.CloudContaId == conta)) : null)
                        ?? Mais(vigentes.Where(f => f.CloudContaId is null));
        return escolhido?.Fator;
    }
}

// ── Visão Geral ──────────────────────────────────────────────────────────────

/// <summary>Linha agregada lida de custo_fato/custo_previsao/custo_credito.</summary>
public sealed record LinhaAgregada(DateOnly Periodo, string Chave, string Chave2, string Moeda, decimal Valor);

public sealed record DadosVisaoGeral(
    IReadOnlyList<LinhaAgregada> ServicoMes,
    IReadOnlyList<LinhaAgregada> ServicoDia,
    IReadOnlyList<LinhaAgregada> CompartimentoMes,
    IReadOnlyList<LinhaAgregada> RecursoMes,
    IReadOnlyList<LinhaAgregada> Previsao,
    IReadOnlyList<LinhaAgregada> Credito,
    DateOnly? DadosAte,
    long? UltimoJob,
    DateTimeOffset? UltimaImportacao,
    IReadOnlyList<FatorCliente> Fatores,
    IReadOnlyDictionary<string, string>? NomesRecursos = null);

public sealed record Kpis(decimal MesCorrente, decimal MesAnterior, decimal Ytd, decimal PrevisaoMes, decimal CreditosMes);
public sealed record PontoMensal(string Mes, string Rotulo, bool Parcial, IReadOnlyDictionary<string, decimal> Valores, decimal Total);
public sealed record PontoDiario(string Dia, IReadOnlyDictionary<string, decimal> Valores);
/// <summary>Chave: identificador técnico (OCID) quando o nome veio do inventário.</summary>
public sealed record ItemValor(string Nome, decimal Valor, string? Detalhe = null, string? Chave = null);

public sealed record VisaoMoeda(string Moeda, Kpis Kpis, IReadOnlyList<string> ServicosMensal, IReadOnlyList<PontoMensal> Mensal,
    IReadOnlyList<ItemValor> Donut, IReadOnlyList<string> ServicosDiario, IReadOnlyList<PontoDiario> Diario,
    IReadOnlyList<ItemValor> TopCompartimentos, IReadOnlyList<ItemValor> TopRecursos);

public sealed record VisaoGeralResposta(long ClienteId, long? ContaId, DateOnly Hoje, DateOnly? DadosAte, long? UltimoJob,
    DateTimeOffset? UltimaImportacao, bool ComFator, bool FatorDisponivel, decimal? FatorAtual, bool Vazio,
    IReadOnlyList<VisaoMoeda> Moedas);

/// <summary>Montagem da Visão Geral a partir das linhas agregadas (funções puras).</summary>
public static class VisaoGeralCustos
{
    public const string Outros = "Outros";
    public const int ServicosEmpilhados = 8;
    public const int ServicosLinha = 5;
    public const int TopN = 10;
    private static readonly string[] Meses = ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

    public static DateOnly PrimeiroDoMes(DateOnly d) => new(d.Year, d.Month, 1);
    public static DateOnly InicioDozeMeses(DateOnly hoje) => PrimeiroDoMes(hoje).AddMonths(-11);
    /// <summary>Diário lido do banco: do 1º dia do mês anterior (cobre os 30 dias e o fallback do mês anterior).</summary>
    public static DateOnly InicioDiario(DateOnly hoje) => PrimeiroDoMes(hoje).AddMonths(-1);

    /// <summary>"Hoje" no fuso de São Paulo.</summary>
    public static DateOnly HojeEmSaoPaulo(TimeProvider relogio) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), FusoSaoPaulo).DateTime);

    public static readonly TimeZoneInfo FusoSaoPaulo = ResolverFuso();

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
    }

    public static VisaoGeralResposta Montar(long clienteId, long? contaId, DateOnly hoje, DadosVisaoGeral d, bool comFatorPedido)
    {
        var fatorAtual = FatorRegras.Vigente(d.Fatores, contaId, hoje);
        var fatorDisponivel = FatorRegras.Disponivel(d.Fatores, contaId, hoje, d.ServicoMes.Select(l => l.Periodo));
        var comFator = comFatorPedido && fatorDisponivel;

        // Multiplica pelo fator vigente na data do período (sem fator naquela data: valor bruto).
        IReadOnlyList<LinhaAgregada> F(IReadOnlyList<LinhaAgregada> linhas) => comFator ? FatorRegras.Aplicar(linhas, d.Fatores, contaId) : linhas;

        var servicoMes = F(d.ServicoMes);
        var servicoDia = F(d.ServicoDia);
        var compMes = F(d.CompartimentoMes);
        var recursoMes = F(d.RecursoMes);
        var previsao = F(d.Previsao);
        var credito = F(d.Credito);

        var mesAtual = PrimeiroDoMes(hoje);
        var moedas = servicoMes.Concat(servicoDia).Concat(compMes).Concat(recursoMes).Concat(previsao).Concat(credito)
            .Select(l => l.Moeda).Distinct().Order(StringComparer.Ordinal).ToList();

        var nomes = d.NomesRecursos ?? new Dictionary<string, string>();
        var visoes = moedas.Select(m => MontarMoeda(m, hoje, mesAtual, d.DadosAte, nomes,
            servicoMes.Where(l => l.Moeda == m).ToList(), servicoDia.Where(l => l.Moeda == m).ToList(),
            compMes.Where(l => l.Moeda == m).ToList(), recursoMes.Where(l => l.Moeda == m).ToList(),
            previsao.Where(l => l.Moeda == m).ToList(), credito.Where(l => l.Moeda == m).ToList())).ToList();

        return new VisaoGeralResposta(clienteId, contaId, hoje, d.DadosAte, d.UltimoJob, d.UltimaImportacao,
            comFator, fatorDisponivel, fatorAtual, visoes.Count == 0, visoes);
    }

    private static VisaoMoeda MontarMoeda(string moeda, DateOnly hoje, DateOnly mesAtual, DateOnly? dadosAte,
        IReadOnlyDictionary<string, string> nomes,
        List<LinhaAgregada> servicoMes, List<LinhaAgregada> servicoDia, List<LinhaAgregada> compMes,
        List<LinhaAgregada> recursoMes, List<LinhaAgregada> previsao, List<LinhaAgregada> credito)
    {
        var mesAnterior = mesAtual.AddMonths(-1);
        var diaMesAtual = servicoDia.Where(l => PrimeiroDoMes(l.Periodo) == mesAtual).ToList();

        // ── KPIs ──
        var realizadoMes = diaMesAtual.Sum(l => l.Valor);
        var anteriorMes = servicoMes.Where(l => l.Periodo == mesAnterior).Sum(l => l.Valor);
        if (anteriorMes == 0)
            anteriorMes = servicoDia.Where(l => PrimeiroDoMes(l.Periodo) == mesAnterior).Sum(l => l.Valor);
        var ytd = servicoMes.Where(l => l.Periodo.Year == hoje.Year).Sum(l => l.Valor);
        var ultimoReal = dadosAte ?? diaMesAtual.Select(l => (DateOnly?)l.Periodo).Max();
        var restante = previsao.Where(l => PrimeiroDoMes(l.Periodo) == mesAtual && (ultimoReal is null || l.Periodo > ultimoReal)).Sum(l => l.Valor);
        var kpis = new Kpis(realizadoMes, anteriorMes, ytd, realizadoMes + restante,
            credito.Where(l => l.Periodo == mesAtual).Sum(l => l.Valor));

        // ── Mensal empilhado por serviço (12 meses; top 8 + Outros) ──
        var topMensal = Top(servicoMes.GroupBy(l => l.Chave).Select(g => (g.Key, g.Sum(l => l.Valor))), ServicosEmpilhados);
        var mensal = Enumerable.Range(0, 12).Select(i => InicioDozeMeses(hoje).AddMonths(i)).Select(mes =>
        {
            var doMes = servicoMes.Where(l => l.Periodo == mes).ToList();
            var valores = Agrupar(doMes.Select(l => (l.Chave, l.Valor)), topMensal);
            return new PontoMensal(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                $"{Meses[mes.Month - 1]}/{mes.Year % 100:D2}", mes == mesAtual, valores, doMes.Sum(l => l.Valor));
        }).ToList();
        var servicosMensal = topMensal.Concat(mensal.Any(p => p.Valores.ContainsKey(Outros)) ? [Outros] : []).ToList();

        // ── Donut do mês corrente por serviço (top 8 + Outros) ──
        var porServicoMes = diaMesAtual.GroupBy(l => l.Chave).Select(g => (g.Key, g.Sum(l => l.Valor))).ToList();
        if (porServicoMes.Count == 0)
            porServicoMes = servicoMes.Where(l => l.Periodo == mesAtual).GroupBy(l => l.Chave).Select(g => (g.Key, g.Sum(l => l.Valor))).ToList();
        var topDonut = Top(porServicoMes, ServicosEmpilhados);
        var donut = Agrupar(porServicoMes, topDonut).Select(kv => new ItemValor(kv.Key, kv.Value))
            .OrderByDescending(i => i.Nome == Outros ? decimal.MinValue : i.Valor).ToList();

        // ── Linha diária: últimos 30 dias, top 5 + Outros ──
        var inicio30 = hoje.AddDays(-29);
        var janela = servicoDia.Where(l => l.Periodo >= inicio30 && l.Periodo <= hoje).ToList();
        var topDiario = Top(janela.GroupBy(l => l.Chave).Select(g => (g.Key, g.Sum(l => l.Valor))), ServicosLinha);
        var diario = Enumerable.Range(0, 30).Select(i => inicio30.AddDays(i))
            .Where(dia => ultimoReal is null || dia <= ultimoReal)
            .Select(dia => new PontoDiario(dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Agrupar(janela.Where(l => l.Periodo == dia).Select(l => (l.Chave, l.Valor)), topDiario, preencher: true)))
            .ToList();
        var servicosDiario = topDiario.Concat(diario.Any(p => p.Valores.GetValueOrDefault(Outros) != 0) ? [Outros] : []).ToList();

        // ── Top 10 compartments e recursos do mês ──
        var topComp = compMes.Where(l => l.Periodo == mesAtual).GroupBy(l => l.Chave)
            .Select(g => new ItemValor(g.Key, g.Sum(l => l.Valor))).OrderByDescending(i => i.Valor).Take(TopN).ToList();
        var topRec = recursoMes.Where(l => l.Periodo == mesAtual).GroupBy(l => (l.Chave, l.Chave2))
            .Select(g => new ItemValor(nomes.GetValueOrDefault(g.Key.Chave) ?? g.Key.Chave, g.Sum(l => l.Valor),
                string.IsNullOrEmpty(g.Key.Chave2) ? null : g.Key.Chave2, g.Key.Chave))
            .OrderByDescending(i => i.Valor).Take(TopN).ToList();

        return new VisaoMoeda(moeda, kpis, servicosMensal, mensal, donut, servicosDiario, diario, topComp, topRec);
    }

    /// <summary>Os N maiores (por valor) em ordem decrescente.</summary>
    public static List<string> Top(IEnumerable<(string Nome, decimal Valor)> itens, int n) =>
        itens.GroupBy(i => i.Nome).Select(g => (Nome: g.Key, Valor: g.Sum(i => i.Valor)))
            .OrderByDescending(i => i.Valor).ThenBy(i => i.Nome, StringComparer.Ordinal).Take(n).Select(i => i.Nome).ToList();

    /// <summary>Soma por nome; o que não está em <paramref name="top"/> vai para "Outros".</summary>
    public static Dictionary<string, decimal> Agrupar(IEnumerable<(string Nome, decimal Valor)> itens, IReadOnlyList<string> top, bool preencher = false)
    {
        var r = new Dictionary<string, decimal>();
        if (preencher)
            foreach (var t in top) r[t] = 0m;
        foreach (var (nome, valor) in itens)
        {
            var k = top.Contains(nome) ? nome : Outros;
            r[k] = r.GetValueOrDefault(k) + valor;
        }
        if (preencher && !r.ContainsKey(Outros)) r[Outros] = 0m;
        return r;
    }
}
