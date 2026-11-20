namespace Finops.Backend.VisaoGeral;

/// <summary>
/// Consultas SQL da Visão Geral (schema svc_finops). Todas parametrizadas e somente leitura.
/// Valores vazios/nulos de serviço, SKU e região viram rótulos fixos, iguais nos filtros e nos resultados.
/// </summary>
public static class VisaoGeralSql
{
    public const string SemServico = "(sem serviço)";
    public const string SemSku = "(sem SKU)";
    public const string SemRegiao = "(sem região)";

    // Expressões de dimensão (mesmas nas listas de filtro, nos WHERE e nos GROUP BY).
    private const string ExprServico = "COALESCE(NULLIF(rn.tipo_recurso, ''), '(sem serviço)')";
    private const string ExprSku = "COALESCE(NULLIF(rn.nome, ''), '(sem SKU)')";
    private const string ExprRegiao = "COALESCE(NULLIF(rn.regiao, ''), '(sem região)')";

    // Filtros de dimensão: array vazio = todos.
    private const string FiltrosDimensao = $@"
  AND (cardinality(@servicos) = 0 OR {ExprServico} = ANY(@servicos))
  AND (cardinality(@skus)     = 0 OR {ExprSku}     = ANY(@skus))
  AND (cardinality(@regioes)  = 0 OR {ExprRegiao}  = ANY(@regioes))";

    // ── Opções (filtros) ─────────────────────────────────────────────────────

    /// <summary>Provedores com o cliente do contrato. Nunca seleciona config_de_acesso.</summary>
    public const string Provedores = @"
SELECT p.id_provedor, p.tipo, p.nome, cl.nome AS cliente
FROM svc_finops.provedor_nuvem p
LEFT JOIN svc_finops.contrato ct ON ct.id_contrato = p.id_contrato
LEFT JOIN svc_finops.cliente  cl ON cl.id_cliente  = ct.id_cliente
ORDER BY p.id_provedor";

    /// <summary>Anos com dados no resumo para o provedor.</summary>
    public const string Anos = @"
SELECT DISTINCT rm.ano::int AS ano
FROM svc_finops.resumo_soma_dia_mes_ano_recurso rm
WHERE rm.cloudproviderid = @provedor
ORDER BY ano DESC";

    public const string Servicos = $@"
SELECT DISTINCT {ExprServico} AS valor
FROM svc_finops.recurso_nuvem rn
WHERE rn.id_provedor = @provedor
ORDER BY valor";

    public const string Skus = $@"
SELECT DISTINCT {ExprSku} AS valor
FROM svc_finops.recurso_nuvem rn
WHERE rn.id_provedor = @provedor
ORDER BY valor";

    public const string Regioes = $@"
SELECT DISTINCT {ExprRegiao} AS valor
FROM svc_finops.recurso_nuvem rn
WHERE rn.id_provedor = @provedor
ORDER BY valor";

    /// <summary>Fator de faturamento do provedor (o cadastro mais recente).</summary>
    public const string Fator = @"
SELECT fc.fator
FROM svc_finops.fator_cliente fc
WHERE fc.id_provedor = @provedor
ORDER BY fc.id DESC
LIMIT 1";

    // ── Visão Geral ──────────────────────────────────────────────────────────

    /// <summary>
    /// Orçamento previsto por ano (periodo) do cliente do provedor.
    /// Soma por período caso exista mais de um orçamento cadastrado para o mesmo cliente/ano.
    /// </summary>
    public const string Orcamentos = @"
SELECT o.periodo, SUM(o.valor_previsto) AS valor_previsto
FROM svc_finops.orcamento o
WHERE o.id_cliente IN (
    SELECT ct.id_cliente
    FROM svc_finops.provedor_nuvem p
    JOIN svc_finops.contrato ct ON ct.id_contrato = p.id_contrato
    WHERE p.id_provedor = @provedor)
  AND o.periodo = ANY(@periodos)
GROUP BY o.periodo";

    /// <summary>
    /// Totais diários no intervalo de meses [@ano_ini/@mes_ini, @ano_fim/@mes_fim], com os filtros.
    /// Base para: custo mensal, acumulado, último mês, mês atual, projeções, 12 meses e custo diário.
    /// </summary>
    public const string TotaisDiarios = $@"
SELECT rm.ano::int AS ano, rm.mes::int AS mes, rm.dia::int AS dia, SUM(rm.total_custo) AS total
FROM svc_finops.resumo_soma_dia_mes_ano_recurso rm
JOIN svc_finops.recurso_nuvem rn ON rn.id_recurso = rm.id_recurso_resumo
WHERE rm.cloudproviderid = @provedor
  AND rn.id_provedor = @provedor
  AND (rm.ano, rm.mes) >= (@ano_ini, @mes_ini)
  AND (rm.ano, rm.mes) <= (@ano_fim, @mes_fim){FiltrosDimensao}
GROUP BY rm.ano, rm.mes, rm.dia";

    /// <summary>
    /// Totais por dia × serviço × SKU × região entre @data_ini (segunda da semana passada) e @data_fim (ontem).
    /// O filtro por (ano, mes) reduz as linhas antes do make_date; o make_date atravessa virada de mês/ano.
    /// Base para: semana anterior e dia anterior (totais + Top 5).
    /// </summary>
    public const string TotaisPorDimensao = $@"
SELECT make_date(rm.ano::int, rm.mes::int, rm.dia::int) AS data,
       {ExprServico} AS servico,
       {ExprSku}     AS sku,
       {ExprRegiao}  AS regiao,
       SUM(rm.total_custo) AS total
FROM svc_finops.resumo_soma_dia_mes_ano_recurso rm
JOIN svc_finops.recurso_nuvem rn ON rn.id_recurso = rm.id_recurso_resumo
WHERE rm.cloudproviderid = @provedor
  AND rn.id_provedor = @provedor
  AND (rm.ano, rm.mes) >= (@ano_ini, @mes_ini)
  AND (rm.ano, rm.mes) <= (@ano_fim, @mes_fim)
  AND make_date(rm.ano::int, rm.mes::int, rm.dia::int) BETWEEN @data_ini AND @data_fim{FiltrosDimensao}
GROUP BY 1, 2, 3, 4";
}
