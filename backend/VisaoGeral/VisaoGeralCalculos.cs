namespace Finops.Backend.VisaoGeral;

/// <summary>
/// Regras de cálculo da Visão Geral. Funções puras (sem banco, sem relógio): recebem os dados
/// brutos e as datas de referência e montam o payload. Cobertas por testes unitários.
/// </summary>
public static class VisaoGeralCalculos
{
    private static readonly string[] NomesMeses =
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

    public const int TamanhoRanking = 5;

    public static string Rotulo(AnoMes am) => $"{NomesMeses[am.Mes - 1]}/{am.Ano % 100:D2}";

    /// <summary>Total por mês a partir dos totais diários.</summary>
    public static Dictionary<AnoMes, decimal> TotaisMensais(IEnumerable<TotalDia> dias) =>
        dias.GroupBy(d => AnoMes.De(d.Data)).ToDictionary(g => g.Key, g => g.Sum(d => d.Total));

    /// <summary>
    /// Projeção do mês: média dos 3 meses fechados anteriores ao atual (mesma lógica do Grafana:
    /// a média considera só os meses que têm dados). Null se nenhum dos 3 tiver dados.
    /// </summary>
    public static decimal? ProjecaoMes(IReadOnlyDictionary<AnoMes, decimal> mensais, DatasReferencia datas)
    {
        var valores = datas.TresMesesFechados.Where(mensais.ContainsKey).Select(m => mensais[m]).ToList();
        return valores.Count == 0 ? null : valores.Average();
    }

    /// <summary>
    /// Projeção anual (corrigida em relação ao Grafana):
    /// ano atual  → soma dos meses fechados do ano + projeção do mês atual + projeção × meses restantes após o atual;
    /// ano passado → total do ano (todos os meses já fechados);
    /// ano futuro → null.
    /// </summary>
    public static decimal? ProjecaoAnual(int ano, IReadOnlyDictionary<AnoMes, decimal> mensais, DatasReferencia datas)
    {
        if (ano < datas.MesAtual.Ano)
            return mensais.Where(kv => kv.Key.Ano == ano).Sum(kv => kv.Value);
        if (ano > datas.MesAtual.Ano)
            return null;

        var projecaoMes = ProjecaoMes(mensais, datas);
        if (projecaoMes is null)
            return null;

        var fechados = mensais.Where(kv => kv.Key.Ano == ano && kv.Key < datas.MesAtual).Sum(kv => kv.Value);
        var mesesRestantes = 12 - datas.MesAtual.Mes;
        return fechados + projecaoMes.Value + projecaoMes.Value * mesesRestantes;
    }

    /// <summary>Faixa de cor de um valor mensal vs orçamento mensal (anual / 12).</summary>
    public static string FaixaOrcamento(decimal valor, decimal? orcamentoMensal)
    {
        if (orcamentoMensal is null || orcamentoMensal <= 0) return Faixa.SemOrcamento;
        var percentual = valor / orcamentoMensal.Value;
        if (percentual <= 0.90m) return Faixa.Ok;
        if (percentual <= 1.00m) return Faixa.Atencao;
        return Faixa.Acima;
    }

    public static decimal? Percentual(decimal? valor, decimal? referencia) =>
        valor is null || referencia is null || referencia <= 0 ? null : Math.Round(valor.Value / referencia.Value * 100m, 1);

    /// <summary>Top N por uma dimensão, do maior para o menor (empate: ordem alfabética).</summary>
    public static List<ItemRanking> Top(IEnumerable<TotalDiaDimensoes> linhas, Func<TotalDiaDimensoes, string> dimensao, int n = TamanhoRanking) =>
        linhas.GroupBy(dimensao)
              .Select(g => new ItemRanking(g.Key, g.Sum(l => l.Total)))
              .OrderByDescending(i => i.Valor).ThenBy(i => i.Nome, StringComparer.Ordinal)
              .Take(n)
              .ToList();

    public static BlocoPeriodo Periodo(IEnumerable<TotalDiaDimensoes> linhas, DateOnly inicio, DateOnly fim)
    {
        var doPeriodo = linhas.Where(l => l.Data >= inicio && l.Data <= fim).ToList();
        return new BlocoPeriodo(
            inicio, fim,
            doPeriodo.Sum(l => l.Total),
            Top(doPeriodo, l => l.Servico),
            Top(doPeriodo, l => l.Sku),
            Top(doPeriodo, l => l.Regiao));
    }

    /// <summary>Monta o payload completo. Com fator, todos os custos são multiplicados pelo fator (o orçamento não).</summary>
    public static VisaoGeralResposta Montar(FiltrosVisaoGeral filtros, DatasReferencia datas, DadosBrutosVisaoGeral dados)
    {
        var fatorAplicado = filtros.ComFator && dados.Fator is > 0 ? dados.Fator.Value : 1m;
        decimal F(decimal v) => v * fatorAplicado;

        var diarios = dados.TotaisDiarios.Select(d => d with { Total = F(d.Total) }).ToList();
        var dimensoes = dados.TotaisPorDimensao.Select(d => d with { Total = F(d.Total) }).ToList();
        var mensais = TotaisMensais(diarios);

        decimal? Orcamento(int ano) => dados.OrcamentoPorAno.TryGetValue(ano, out var v) && v > 0 ? v : null;
        decimal? OrcamentoMensal(int ano) => Orcamento(ano) is { } a ? a / 12m : null;

        var mesesFiltro = filtros.Meses.Count == 0 ? Enumerable.Range(1, 12).ToHashSet() : filtros.Meses.ToHashSet();
        bool MesNoFiltro(AnoMes am) => mesesFiltro.Contains(am.Mes);

        // ── Indicadores ──
        var orcamentoAno = Orcamento(filtros.Ano);
        var acumulado = mensais.Where(kv => kv.Key.Ano == filtros.Ano && MesNoFiltro(kv.Key)).Sum(kv => kv.Value);
        var ultimoMes = mensais.GetValueOrDefault(datas.UltimoMesFechado);
        var mesAtual = mensais.GetValueOrDefault(datas.MesAtual);
        var projecaoMes = ProjecaoMes(mensais, datas);
        var projecaoAnual = ProjecaoAnual(datas.MesAtual.Ano, mensais, datas);

        var indicadores = new Indicadores(
            new Indicador(orcamentoAno, orcamentoAno is null ? null : 100m, filtros.Ano.ToString()),
            new Indicador(acumulado, Percentual(acumulado, orcamentoAno), filtros.Ano.ToString()),
            new Indicador(ultimoMes, Percentual(ultimoMes, OrcamentoMensal(datas.UltimoMesFechado.Ano)), Rotulo(datas.UltimoMesFechado)),
            new Indicador(mesAtual, Percentual(mesAtual, OrcamentoMensal(datas.MesAtual.Ano)), Rotulo(datas.MesAtual)),
            new Indicador(projecaoMes, Percentual(projecaoMes, OrcamentoMensal(datas.MesAtual.Ano)), Rotulo(datas.MesAtual)),
            new Indicador(projecaoAnual, Percentual(projecaoAnual, Orcamento(datas.MesAtual.Ano)), datas.MesAtual.Ano.ToString()));

        // ── Custo mensal do ano selecionado (+ barra de projeção do mês atual) ──
        var orcamentoMensalAno = OrcamentoMensal(filtros.Ano);
        var barras = mensais
            .Where(kv => kv.Key.Ano == filtros.Ano && MesNoFiltro(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => new BarraMensal(kv.Key.Ano, kv.Key.Mes, Rotulo(kv.Key), kv.Value, false, FaixaOrcamento(kv.Value, orcamentoMensalAno)))
            .ToList();
        if (filtros.Ano == datas.MesAtual.Ano && MesNoFiltro(datas.MesAtual) && projecaoMes is { } pm)
        {
            barras.Add(new BarraMensal(datas.MesAtual.Ano, datas.MesAtual.Mes, "Projeção mês atual", pm, true,
                FaixaOrcamento(pm, orcamentoMensalAno)));
        }

        // ── Últimos 12 meses fechados + mês atual ──
        var ultimos12 = datas.UltimosDozeMesesEAtual
            .Select(am => new PontoMensal(am.Ano, am.Mes, Rotulo(am), mensais.GetValueOrDefault(am), am == datas.MesAtual))
            .ToList();

        // ── Semana anterior (seg–dom) e dia anterior (ontem) ──
        var semana = Periodo(dimensoes, datas.SemanaAnteriorInicio, datas.SemanaAnteriorFim);
        var dia = Periodo(dimensoes, datas.Ontem, datas.Ontem);

        // ── Custo diário do mês corrente ──
        var diario = diarios
            .Where(d => AnoMes.De(d.Data) == datas.MesAtual)
            .GroupBy(d => d.Data.Day)
            .OrderBy(g => g.Key)
            .Select(g => new PontoDiario(g.Key, g.Sum(d => d.Total)))
            .ToList();

        return new VisaoGeralResposta(
            filtros.Provedor,
            dados.NomeProvedor,
            filtros.Ano,
            filtros.ComFator && fatorAplicado != 1m,
            dados.Fator,
            new ReferenciasVisaoGeral(datas.Hoje, datas.MesAtual.ToString(), datas.UltimoMesFechado.ToString(),
                datas.SemanaAnteriorInicio, datas.SemanaAnteriorFim, datas.Ontem),
            orcamentoMensalAno,
            indicadores,
            barras,
            ultimos12,
            semana,
            dia,
            diario);
    }

    /// <summary>Intervalo de meses que a consulta de totais diários precisa cobrir.</summary>
    public static (AnoMes Inicio, AnoMes Fim) IntervaloMeses(int anoSelecionado, DatasReferencia datas)
    {
        var inicioAno = new AnoMes(anoSelecionado, 1);
        var inicio12 = datas.UltimosDozeMesesEAtual[0];
        var inicio = inicioAno < inicio12 ? inicioAno : inicio12;

        var fimAno = new AnoMes(anoSelecionado, 12);
        var fim = fimAno > datas.MesAtual ? fimAno : datas.MesAtual;
        return (inicio, fim);
    }
}
