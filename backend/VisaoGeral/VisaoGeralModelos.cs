namespace Finops.Backend.VisaoGeral;

// ── Entrada ──────────────────────────────────────────────────────────────────

/// <summary>Filtros da Visão Geral. Listas vazias = "todos".</summary>
public sealed record FiltrosVisaoGeral(
    int Provedor,
    int Ano,
    IReadOnlyList<int> Meses,
    IReadOnlyList<string> Servicos,
    IReadOnlyList<string> Skus,
    IReadOnlyList<string> Regioes,
    bool ComFator)
{
    /// <summary>Chave estável para cache (ordem dos itens não importa).</summary>
    public string ChaveCache(DateOnly hoje) => string.Join("|",
        "vg", hoje.ToString("yyyy-MM-dd"), Provedor, Ano, ComFator,
        string.Join(",", Meses.Order()),
        string.Join("\u001f", Servicos.Order(StringComparer.Ordinal)),
        string.Join("\u001f", Skus.Order(StringComparer.Ordinal)),
        string.Join("\u001f", Regioes.Order(StringComparer.Ordinal)));
}

// ── Linhas lidas do banco ────────────────────────────────────────────────────

/// <summary>Total de um dia (já filtrado por provedor/serviço/SKU/região).</summary>
public sealed record TotalDia(DateOnly Data, decimal Total);

/// <summary>Total de um dia por serviço/SKU/região (semana anterior e ontem).</summary>
public sealed record TotalDiaDimensoes(DateOnly Data, string Servico, string Sku, string Regiao, decimal Total);

/// <summary>Dados brutos que a Visão Geral precisa do banco.</summary>
public sealed record DadosBrutosVisaoGeral(
    IReadOnlyList<TotalDia> TotaisDiarios,
    IReadOnlyList<TotalDiaDimensoes> TotaisPorDimensao,
    IReadOnlyDictionary<int, decimal> OrcamentoPorAno,
    decimal? Fator,
    string? NomeProvedor);

// ── Saída ────────────────────────────────────────────────────────────────────

public sealed record ProvedorOpcao(int Id, string Tipo, string? Nome, string? Cliente, string Rotulo);

public sealed record OpcoesVisaoGeral(
    IReadOnlyList<ProvedorOpcao> Provedores,
    int? ProvedorSelecionado,
    IReadOnlyList<int> Anos,
    IReadOnlyList<string> Servicos,
    IReadOnlyList<string> Skus,
    IReadOnlyList<string> Regioes,
    decimal? Fator);

public sealed record Indicador(decimal? Valor, decimal? PercentualOrcamento, string? Referencia);

public sealed record Indicadores(
    Indicador OrcamentoAnual,
    Indicador AcumuladoAno,
    Indicador UltimoMes,
    Indicador MesAtual,
    Indicador ProjecaoMes,
    Indicador ProjecaoAnual);

/// <summary>Faixa de cor de uma barra vs orçamento mensal: ok (≤90%), atencao (≤100%), acima (&gt;100%), sem_orcamento.</summary>
public static class Faixa
{
    public const string Ok = "ok";
    public const string Atencao = "atencao";
    public const string Acima = "acima";
    public const string SemOrcamento = "sem_orcamento";
}

public sealed record BarraMensal(int Ano, int Mes, string Rotulo, decimal Valor, bool Projecao, string Faixa);

public sealed record PontoMensal(int Ano, int Mes, string Rotulo, decimal Valor, bool Parcial);

public sealed record ItemRanking(string Nome, decimal Valor);

public sealed record BlocoPeriodo(
    DateOnly Inicio,
    DateOnly Fim,
    decimal Total,
    IReadOnlyList<ItemRanking> TopServicos,
    IReadOnlyList<ItemRanking> TopSkus,
    IReadOnlyList<ItemRanking> TopRegioes);

public sealed record PontoDiario(int Dia, decimal Valor);

public sealed record ReferenciasVisaoGeral(
    DateOnly Hoje,
    string MesAtual,
    string UltimoMesFechado,
    DateOnly SemanaAnteriorInicio,
    DateOnly SemanaAnteriorFim,
    DateOnly Ontem);

public sealed record VisaoGeralResposta(
    int Provedor,
    string? NomeProvedor,
    int Ano,
    bool ComFator,
    decimal? Fator,
    ReferenciasVisaoGeral Referencias,
    decimal? OrcamentoMensal,
    Indicadores Indicadores,
    IReadOnlyList<BarraMensal> CustoMensal,
    IReadOnlyList<PontoMensal> UltimosDozeMeses,
    BlocoPeriodo SemanaAnterior,
    BlocoPeriodo DiaAnterior,
    IReadOnlyList<PontoDiario> CustoDiario);
