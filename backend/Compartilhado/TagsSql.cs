// Fragmentos SQL de tags compartilhados entre a API e o cloudvista-worker (uma regra só).
// O worker compila este arquivo por link (CloudVista.Worker.csproj, constante CLOUDVISTA_WORKER),
// com outro namespace: os testes referenciam os dois assemblies sem ambiguidade.
#if CLOUDVISTA_WORKER
namespace CloudVista.Worker;
#else
namespace Finops.Backend.Compartilhado;
#endif

/// <summary>
/// Tag automática da Oracle: namespace "Oracle-Tags" ou "*-Oracle-Tags" com CreatedBy/CreatedOn
/// (espelha GovernancaRegras.EhAutomatica). Automáticas nunca contam como "tem tag" nem satisfazem política.
/// </summary>
public static class TagsSql
{
    /// <summary>Sobre os aliases d (namespace defined) e k (chave dentro do namespace).</summary>
    public const string Automatica = "(d.key = 'Oracle-Tags' OR d.key LIKE '%-Oracle-Tags') AND k.key IN ('CreatedBy', 'CreatedOn')";

    public const string Namespace = "CASE WHEN jsonb_typeof(d.value) = 'object' THEN d.value ELSE '{}'::jsonb END";

    /// <summary>Recurso (alias r) tem ao menos uma tag que não é automática (defined ou freeform).</summary>
    public const string TemTagNaoAutomaticaR =
        "(EXISTS (SELECT 1 FROM jsonb_each(CASE WHEN jsonb_typeof(r.tags->'defined') = 'object' THEN r.tags->'defined' ELSE '{}'::jsonb END) AS d" +
        " CROSS JOIN LATERAL jsonb_each(" + Namespace + ") AS k WHERE NOT (" + Automatica + "))" +
        " OR EXISTS (SELECT 1 FROM jsonb_each(CASE WHEN jsonb_typeof(r.tags->'freeform') = 'object' THEN r.tags->'freeform' ELSE '{}'::jsonb END) AS f))";

    /// <summary>
    /// Acentos tratados na normalização SQL (unaccent indisponível). Mesmo resultado de
    /// GovernancaRegras.Normalizar para estes caracteres (teste cruzado).
    /// </summary>
    public const string AcentosDe = "áàâãäåéèêëíìîïóòôõöúùûüçñýÿÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝ";
    public const string AcentosPara = "aaaaaaeeeeiiiiooooouuuucnyyaaaaaaeeeeiiiiooooouuuucny";

    /// <summary>Expressão SQL que normaliza um valor: sem acento, minúsculo, espaços colapsados.</summary>
    public static string Norm(string expr) =>
        $"regexp_replace(lower(translate(btrim({expr}), '{AcentosDe}', '{AcentosPara}')), '\\s+', ' ', 'g')";
}

/// <summary>
/// Conformidade com a política de tags do cliente (tag_politica). Recurso conforme: para TODA chave
/// ativa, tem a tag (defined ns.k ou freeform k, não automática) com valor não vazio e, se houver
/// valores_permitidos, com valor da lista (comparação normalizada).
/// </summary>
public static class PoliticaSql
{
    /// <summary>Pares (recurso, tag, valor) não automáticos dos recursos de "base".</summary>
    public static readonly string ParesNaoAutomaticos = @"
pares AS (
    SELECT b.cloud_conta_id, b.chave, d.key || '.' || k.key AS tag, k.value #>> '{}' AS valor
    FROM base b
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(b.tags->'defined') = 'object' THEN b.tags->'defined' ELSE '{}'::jsonb END) AS d
    CROSS JOIN LATERAL jsonb_each(" + TagsSql.Namespace + @") AS k
    WHERE NOT (" + TagsSql.Automatica + @")
    UNION ALL
    SELECT b.cloud_conta_id, b.chave, f.key AS tag, f.value #>> '{}' AS valor
    FROM base b
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(b.tags->'freeform') = 'object' THEN b.tags->'freeform' ELSE '{}'::jsonb END) AS f
)";

    /// <summary>Chaves ativas da política com os valores permitidos já normalizados.</summary>
    public static readonly string Pol = @"
pol AS (
    SELECT p.chave AS tag, p.valores_permitidos IS NOT NULL AS restrita,
           ARRAY(SELECT " + TagsSql.Norm("x") + @" FROM unnest(p.valores_permitidos) AS x) AS permitidos
    FROM tag_politica p
    WHERE p.cliente_id = @cliente_id AND p.ativa
)";

    /// <summary>Por recurso × chave da política: presente (valor não vazio) e ok (presente e dentro da lista).</summary>
    public static readonly string Aval = @"
aval AS (
    SELECT b.cloud_conta_id, b.chave AS rec, b.tipo, b.caminho, pol.tag,
           COALESCE(bool_or(pr.valor IS NOT NULL AND btrim(pr.valor) <> ''), false) AS presente,
           COALESCE(bool_or(pr.valor IS NOT NULL AND btrim(pr.valor) <> ''
                    AND (NOT pol.restrita OR " + TagsSql.Norm("pr.valor") + @" = ANY(pol.permitidos))), false) AS ok
    FROM base b
    CROSS JOIN pol
    LEFT JOIN pares pr ON pr.cloud_conta_id = b.cloud_conta_id AND pr.chave = b.chave AND pr.tag = pol.tag
    GROUP BY b.cloud_conta_id, b.chave, b.tipo, b.caminho, pol.tag
)";

    /// <summary>Recursos ativos do cliente inteiro (snapshot do worker e status da política).</summary>
    public const string BaseCliente = @"
base AS (
    SELECT r.cloud_conta_id, r.chave, r.tipo, r.tags, c.caminho
    FROM recurso r
    LEFT JOIN compartimento c ON c.cloud_conta_id = r.cloud_conta_id AND c.ocid = r.compartimento_ocid
    WHERE r.cliente_id = @cliente_id AND r.removido_em IS NULL
)";

    public static readonly string Com = "WITH " + BaseCliente + "," + ParesNaoAutomaticos + "," + Pol + "," + Aval;

    /// <summary>Uma linha: total, conformes, com alguma tag, por chave (jsonb), chaves ativas.</summary>
    public static readonly string Resumo = Com + @"
SELECT (SELECT count(*) FROM base) AS total,
       (SELECT count(*) FROM (SELECT cloud_conta_id, rec FROM aval GROUP BY cloud_conta_id, rec HAVING bool_and(ok)) x) AS conformes,
       (SELECT count(*) FROM (SELECT DISTINCT cloud_conta_id, chave FROM pares) y) AS com_alguma_tag,
       (SELECT COALESCE(jsonb_object_agg(tag, jsonb_build_object('presentes', presentes, 'fora_da_lista', fora)), '{}'::jsonb)
          FROM (SELECT tag, count(*) FILTER (WHERE presente) AS presentes, count(*) FILTER (WHERE presente AND NOT ok) AS fora
                FROM aval GROUP BY tag) z) AS por_chave,
       (SELECT COALESCE(array_agg(tag ORDER BY tag), ARRAY[]::text[]) FROM pol) AS chaves";

    /// <summary>
    /// Snapshot diário (worker): dia = data de @visto em America/Sao_Paulo; o último cálculo do dia vence.
    /// Sem política ativa: não grava nada.
    /// </summary>
    public static readonly string UpsertSnapshot = Com + @",
res AS (
    SELECT (SELECT count(*) FROM base) AS total,
           (SELECT count(*) FROM (SELECT cloud_conta_id, rec FROM aval GROUP BY cloud_conta_id, rec HAVING bool_and(ok)) x) AS conformes,
           (SELECT count(*) FROM (SELECT DISTINCT cloud_conta_id, chave FROM pares) y) AS com_alguma_tag,
           (SELECT COALESCE(jsonb_object_agg(tag, jsonb_build_object('presentes', presentes, 'fora_da_lista', fora)), '{}'::jsonb)
              FROM (SELECT tag, count(*) FILTER (WHERE presente) AS presentes, count(*) FILTER (WHERE presente AND NOT ok) AS fora
                    FROM aval GROUP BY tag) z) AS por_chave,
           (SELECT array_agg(tag ORDER BY tag) FROM pol) AS chaves
)
INSERT INTO tag_conformidade_dia (cliente_id, dia, total, conformes, com_alguma_tag, por_chave, chaves, calculado_em)
SELECT @cliente_id, CAST(timezone('America/Sao_Paulo', @visto) AS date), res.total, res.conformes, res.com_alguma_tag,
       res.por_chave, res.chaves, now()
FROM res
WHERE EXISTS (SELECT 1 FROM pol)
ON CONFLICT (cliente_id, dia) DO UPDATE
SET total = EXCLUDED.total, conformes = EXCLUDED.conformes, com_alguma_tag = EXCLUDED.com_alguma_tag,
    por_chave = EXCLUDED.por_chave, chaves = EXCLUDED.chaves, calculado_em = EXCLUDED.calculado_em";

    /// <summary>
    /// Clientes cujo snapshot de hoje precisa ser recalculado: política alterada depois do cálculo de
    /// hoje, ou sem snapshot de hoje mas com inventário. Só clientes com alguma chave ativa.
    /// </summary>
    public const string ClientesParaRecalcular = @"
SELECT p.cliente_id
FROM tag_politica p
LEFT JOIN tag_conformidade_dia s
       ON s.cliente_id = p.cliente_id AND s.dia = CAST(timezone('America/Sao_Paulo', now()) AS date)
GROUP BY p.cliente_id, s.calculado_em
HAVING bool_or(p.ativa)
   AND (max(COALESCE(p.atualizado_em, p.criado_em)) > COALESCE(s.calculado_em, '-infinity'::timestamptz)
        OR (s.calculado_em IS NULL AND EXISTS (SELECT 1 FROM recurso r WHERE r.cliente_id = p.cliente_id AND r.removido_em IS NULL)))
LIMIT 50";
}
