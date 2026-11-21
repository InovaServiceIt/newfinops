using System.Text.Json;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Finops.Backend.Inventario;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Governanca;

/// <summary>
/// SQL da governança de tags: só leitura de recurso.tags (recursos ativos), parametrizado,
/// sem interpolar filtros. RLS do Inventário (app_cliente_visivel).
/// </summary>
public static class GovernancaSql
{
    /// <summary>Recursos ativos do filtro.</summary>
    private const string Base = @"
base AS (
    SELECT r.cloud_conta_id, r.chave, r.tipo, r.tags, c.caminho
    FROM recurso r
    LEFT JOIN compartimento c ON c.cloud_conta_id = r.cloud_conta_id AND c.ocid = r.compartimento_ocid
    WHERE r.cliente_id = @cliente_id AND (@conta_id IS NULL OR r.cloud_conta_id = @conta_id)
      AND r.removido_em IS NULL
      AND (@regiao IS NULL OR r.regiao = @regiao)
      AND (@tipo IS NULL OR r.tipo = @tipo)
      AND (@compartimento IS NULL OR c.caminho = @compartimento OR c.caminho LIKE @compartimento_prefixo ESCAPE '\')
)";

    /// <summary>
    /// Pares (recurso, tag, valor): defined "ns.chave" (2 níveis) + freeform "chave".
    /// Automáticas (Oracle-Tags / *-Oracle-Tags com CreatedBy/CreatedOn) só com @automaticas.
    /// </summary>
    private const string Pares = @"
pares AS (
    SELECT b.cloud_conta_id, b.chave, d.key || '.' || k.key AS tag, 'DEFINED' AS origem, k.value #>> '{}' AS valor
    FROM base b
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(b.tags->'defined') = 'object' THEN b.tags->'defined' ELSE '{}'::jsonb END) AS d
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(d.value) = 'object' THEN d.value ELSE '{}'::jsonb END) AS k
    WHERE @automaticas
       OR NOT ((d.key = 'Oracle-Tags' OR d.key LIKE '%-Oracle-Tags') AND k.key IN ('CreatedBy', 'CreatedOn'))
    UNION ALL
    SELECT b.cloud_conta_id, b.chave, f.key AS tag, 'FREEFORM' AS origem, f.value #>> '{}' AS valor
    FROM base b
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(b.tags->'freeform') = 'object' THEN b.tags->'freeform' ELSE '{}'::jsonb END) AS f
)";

    private const string Com = "WITH " + Base + "," + Pares;

    public const string Totais = Com + @"
SELECT (SELECT count(*) FROM base),
       (SELECT count(*) FROM (SELECT DISTINCT cloud_conta_id, chave FROM pares) x)";

    public const string Chaves = Com + @"
SELECT tag, origem, count(DISTINCT (cloud_conta_id, chave)) AS recursos, count(DISTINCT valor) AS distintos
FROM pares
GROUP BY tag, origem
ORDER BY recursos DESC, tag, origem
LIMIT @limite";

    public const string TopValores = Com + @"
SELECT tag, origem, valor, n FROM (
    SELECT tag, origem, valor, count(*) AS n,
           row_number() OVER (PARTITION BY tag, origem ORDER BY count(*) DESC, valor) AS rn
    FROM pares
    GROUP BY tag, origem, valor
) x
WHERE rn <= @top";

    public const string CoberturaPorTipo = Com + @"
SELECT b.tipo, count(*),
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM pares p WHERE p.cloud_conta_id = b.cloud_conta_id AND p.chave = b.chave))
FROM base b
GROUP BY b.tipo
ORDER BY count(*) DESC, b.tipo";

    /// <summary>Por recurso: chaves selecionadas ausentes (presença conta qualquer origem, inclusive automáticas).</summary>
    private const string Avaliacao = Com + @",
presentes AS (SELECT DISTINCT cloud_conta_id, chave, tag FROM pares),
avaliacao AS (
    SELECT b.cloud_conta_id, b.chave, b.tipo, b.caminho,
           array_remove(array_agg(CASE WHEN p.tag IS NULL THEN k.tag END ORDER BY k.tag), NULL) AS faltando
    FROM base b
    CROSS JOIN unnest(CAST(@chaves AS text[])) AS k(tag)
    LEFT JOIN presentes p ON p.cloud_conta_id = b.cloud_conta_id AND p.chave = b.chave AND p.tag = k.tag
    GROUP BY b.cloud_conta_id, b.chave, b.tipo, b.caminho
)";

    public const string AvaliacaoGeral = Avaliacao + @"
SELECT count(*), count(*) FILTER (WHERE cardinality(faltando) = 0) FROM avaliacao";

    public const string AvaliacaoPorChave = Avaliacao + @"
SELECT k.tag, count(*) FILTER (WHERE NOT (k.tag = ANY(a.faltando))), count(*) FILTER (WHERE k.tag = ANY(a.faltando))
FROM avaliacao a CROSS JOIN unnest(CAST(@chaves AS text[])) AS k(tag)
GROUP BY k.tag
ORDER BY k.tag";

    public const string AvaliacaoPorCompartimento = Avaliacao + @"
SELECT COALESCE(caminho, ''), count(*), count(*) FILTER (WHERE cardinality(faltando) > 0) AS n
FROM avaliacao GROUP BY 1 ORDER BY n DESC, 1 LIMIT @top";

    public const string AvaliacaoPorTipo = Avaliacao + @"
SELECT tipo, count(*), count(*) FILTER (WHERE cardinality(faltando) > 0) AS n
FROM avaliacao GROUP BY 1 ORDER BY n DESC, 1 LIMIT @top";

    private const string NaoConformesWhere = @"
FROM avaliacao a
JOIN recurso r ON r.cloud_conta_id = a.cloud_conta_id AND r.chave = a.chave
WHERE cardinality(a.faltando) > 0 AND (@faltando IS NULL OR @faltando = ANY(a.faltando))";

    public const string ContarNaoConformes = Avaliacao + "\nSELECT count(*)" + NaoConformesWhere;

    public const string ListarNaoConformes = Avaliacao + @"
SELECT r.cloud_conta_id, r.chave, r.nome, r.tipo, r.regiao, a.caminho, r.estado, r.criado_em_cloud, r.tags::text,
       r.ultimo_visto_em, a.faltando" + NaoConformesWhere + @"
ORDER BY a.caminho NULLS LAST, lower(r.nome), r.chave
LIMIT @limite OFFSET @deslocamento";

    public static readonly string ExportarNaoConformes = Avaliacao + @"
SELECT cc.nome, r.nome, r.chave, r.tipo, r.regiao, a.caminho, r.estado, array_to_string(a.faltando, ', '),
       r.ultimo_visto_em, r.tags::text" + NaoConformesWhere.Replace("JOIN recurso r ON", "JOIN cloud_conta cc ON cc.id = a.cloud_conta_id\nJOIN recurso r ON") + @"
ORDER BY cc.nome, a.caminho NULLS LAST, lower(r.nome), r.chave
LIMIT @limite";

    /// <summary>Valores exatos da chave (a normalização sem acento é feita em C#: unaccent indisponível).</summary>
    public const string Valores = Com + @"
SELECT valor, count(*) FROM pares WHERE tag = @chave AND valor IS NOT NULL GROUP BY valor";
}

public sealed class GovernancaRepositorio(CloudVistaDb db)
{
    public Task<ResumoGovernanca> ResumoAsync(UsuarioAtual u, FiltroGovernanca f, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            long total = 0, comTag = 0;
            await using (var cmd = Comando(conn, tx, GovernancaSql.Totais, f))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                if (await r.ReadAsync(ct)) { total = r.GetInt64(0); comTag = r.GetInt64(1); }

            var linhas = new List<(string Tag, string Origem, long Recursos, long Distintos)>();
            await using (var cmd = Comando(conn, tx, GovernancaSql.Chaves, f))
            {
                cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = GovernancaRegras.MaxChavesResumo + 1 });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) linhas.Add((r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3)));
            }
            var truncado = linhas.Count > GovernancaRegras.MaxChavesResumo;
            if (truncado) linhas.RemoveAt(linhas.Count - 1);

            var top = new Dictionary<(string, string), List<ValorContagem>>();
            await using (var cmd = Comando(conn, tx, GovernancaSql.TopValores, f))
            {
                cmd.Parameters.Add(new NpgsqlParameter("top", NpgsqlDbType.Integer) { Value = GovernancaRegras.TopValores });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var k = (r.GetString(0), r.GetString(1));
                    if (!top.TryGetValue(k, out var l)) top[k] = l = [];
                    l.Add(new ValorContagem(r.IsDBNull(2) ? "" : r.GetString(2), r.GetInt64(3)));
                }
            }

            var chaves = linhas.Select(l => new ChaveTag(l.Tag, l.Origem, l.Recursos, GovernancaRegras.Pct(l.Recursos, total), l.Distintos,
                top.GetValueOrDefault((l.Tag, l.Origem)) ?? [])).ToList();

            var porTipo = new List<CoberturaTipo>();
            await using (var cmd = Comando(conn, tx, GovernancaSql.CoberturaPorTipo, f))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) porTipo.Add(new CoberturaTipo(r.GetString(0), r.GetInt64(1), r.GetInt64(2)));

            var prov = new List<ProvenienciaConta>();
            await using (var cmd = new NpgsqlCommand(InventarioSql.Proveniencia, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = f.ClienteId });
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)f.ContaId ?? DBNull.Value });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    prov.Add(new ProvenienciaConta(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetFieldValue<DateTimeOffset>(2), r.IsDBNull(3) ? null : r.GetInt64(3)));
            }

            return new ResumoGovernanca(total, comTag, total - comTag, chaves, truncado, porTipo, prov);
        }, ct);

    public Task<AvaliacaoGovernanca> AvaliarAsync(UsuarioAtual u, FiltroGovernanca f, string[] chaves, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            NpgsqlCommand Cmd(string sql)
            {
                var c = Comando(conn, tx, sql, f, chaves);
                c.Parameters.Add(new NpgsqlParameter("top", NpgsqlDbType.Integer) { Value = GovernancaRegras.TopGrupos });
                return c;
            }

            long total = 0, conformes = 0;
            await using (var cmd = Cmd(GovernancaSql.AvaliacaoGeral))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                if (await r.ReadAsync(ct)) { total = r.GetInt64(0); conformes = r.GetInt64(1); }

            var porChave = new List<PresencaChave>();
            await using (var cmd = Cmd(GovernancaSql.AvaliacaoPorChave))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) porChave.Add(new PresencaChave(r.GetString(0), r.GetInt64(1), r.GetInt64(2)));

            async Task<List<GrupoNaoConforme>> Grupos(string sql)
            {
                var l = new List<GrupoNaoConforme>();
                await using var cmd = Cmd(sql);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    if (r.GetInt64(2) > 0) l.Add(new GrupoNaoConforme(r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
                return l;
            }

            var porComp = await Grupos(GovernancaSql.AvaliacaoPorCompartimento);
            var porTipo = await Grupos(GovernancaSql.AvaliacaoPorTipo);
            return new AvaliacaoGovernanca(total, conformes, total - conformes, GovernancaRegras.Pct(conformes, total), porChave, porComp, porTipo);
        }, ct);

    public Task<List<GrupoVariacao>> VariacoesAsync(UsuarioAtual u, FiltroGovernanca f, string chave, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            var valores = new List<Grafia>();
            await using var cmd = Comando(conn, tx, GovernancaSql.Valores, f with { Automaticas = true });
            cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Text) { Value = chave });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) valores.Add(new Grafia(r.GetString(0), r.GetInt64(1)));
            return GovernancaRegras.Variacoes(valores);
        }, ct);

    public Task<PaginaNaoConformes> NaoConformesAsync(UsuarioAtual u, FiltroGovernanca f, string[] chaves, string? faltando, int pagina, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            long total;
            await using (var cmd = ComandoNc(conn, tx, GovernancaSql.ContarNaoConformes, f, chaves, faltando))
                total = (long)(await cmd.ExecuteScalarAsync(ct))!;

            var itens = new List<NaoConformeItem>();
            await using (var cmd = ComandoNc(conn, tx, GovernancaSql.ListarNaoConformes, f, chaves, faltando))
            {
                cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = InventarioRegras.TamanhoPagina });
                cmd.Parameters.Add(new NpgsqlParameter("deslocamento", NpgsqlDbType.Integer) { Value = (pagina - 1) * InventarioRegras.TamanhoPagina });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    using var tags = JsonDocument.Parse(r.IsDBNull(8) ? "{}" : r.GetString(8));
                    itens.Add(new NaoConformeItem(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                        r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6),
                        r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), tags.RootElement.Clone(),
                        r.GetFieldValue<DateTimeOffset>(9), r.GetFieldValue<string[]>(10)));
                }
            }
            return new PaginaNaoConformes(itens, total, pagina, InventarioRegras.TamanhoPagina);
        }, ct);

    public Task<List<string>> ExportarAsync(UsuarioAtual u, FiltroGovernanca f, string[] chaves, string? faltando, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            var linhas = new List<string> { InventarioRegras.LinhaCsv(GovernancaRegras.CabecalhoCsv) };
            await using var cmd = ComandoNc(conn, tx, GovernancaSql.ExportarNaoConformes, f, chaves, faltando);
            cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = InventarioRegras.LimiteCsv });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                linhas.Add(InventarioRegras.LinhaCsv([
                    r.IsDBNull(0) ? null : r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7),
                    InventarioRegras.Data(r.GetFieldValue<DateTimeOffset>(8)), r.IsDBNull(9) ? null : r.GetString(9)]));
            return linhas;
        }, ct);

    // ── auxiliares ──────────────────────────────────────────────────────────

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, FiltroGovernanca f, string[]? chaves = null)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        void Texto(string nome, string? valor) =>
            cmd.Parameters.Add(new NpgsqlParameter(nome, NpgsqlDbType.Text) { Value = (object?)valor ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = f.ClienteId });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)f.ContaId ?? DBNull.Value });
        Texto("regiao", f.Regiao);
        Texto("tipo", f.Tipo);
        Texto("compartimento", f.Compartimento);
        Texto("compartimento_prefixo", f.Compartimento is null ? null : InventarioRegras.EscaparLike(f.Compartimento) + "/%");
        // Na avaliação a presença considera também as automáticas (presença é presença).
        cmd.Parameters.Add(new NpgsqlParameter("automaticas", NpgsqlDbType.Boolean) { Value = f.Automaticas || chaves is not null });
        if (chaves is not null)
            cmd.Parameters.Add(new NpgsqlParameter("chaves", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = chaves });
        return cmd;
    }

    private static NpgsqlCommand ComandoNc(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, FiltroGovernanca f, string[] chaves, string? faltando)
    {
        var cmd = Comando(conn, tx, sql, f, chaves);
        cmd.Parameters.Add(new NpgsqlParameter("faltando", NpgsqlDbType.Text) { Value = (object?)faltando ?? DBNull.Value });
        return cmd;
    }
}
