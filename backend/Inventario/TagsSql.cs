namespace Finops.Backend.Inventario;

/// <summary>
/// Fragmentos SQL de tags compartilhados por Inventário e Governança (uma regra só).
/// Tag automática da Oracle: namespace "Oracle-Tags" ou "*-Oracle-Tags" com CreatedBy/CreatedOn
/// (espelha GovernancaRegras.EhAutomatica).
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
}
