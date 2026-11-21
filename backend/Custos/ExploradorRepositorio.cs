using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Finops.Backend.Inventario;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Custos;

/// <summary>SQL do Explorador: agregação no banco (GROUP BY), parametrizado, sem SELECT *. RLS por cliente.</summary>
public static class ExploradorSql
{
    private const string Filtro = @"
f.cliente_id = @cliente_id AND (@conta_id IS NULL OR f.cloud_conta_id = @conta_id)
  AND f.dimensao = @dimensao AND f.granularidade = @granularidade
  AND (@tag_chave IS NULL OR f.chave2 = @tag_chave)
  AND (cardinality(@filtro) = 0 OR f.chave = ANY(@filtro))";

    /// <summary>Período atual e anterior numa ida só (o C# separa); RECURSO soma os serviços (chave2).</summary>
    public const string Consulta = @"
SELECT f.periodo, f.chave, f.moeda, sum(f.valor)
FROM custo_fato f
WHERE " + Filtro + @"
  AND f.periodo BETWEEN @de AND @ate
GROUP BY f.periodo, f.chave, f.moeda";

    public const string Creditos = @"
SELECT c.mes, c.moeda, sum(c.valor)
FROM custo_credito c
WHERE c.cliente_id = @cliente_id AND (@conta_id IS NULL OR c.cloud_conta_id = @conta_id)
  AND c.mes BETWEEN @de AND @ate
GROUP BY c.mes, c.moeda";

    public const string Proveniencia = @"
SELECT DISTINCT ON (f.cloud_conta_id) f.cloud_conta_id, cc.nome, f.atualizado_em, f.job_execucao_id
FROM custo_fato f
JOIN cloud_conta cc ON cc.id = f.cloud_conta_id
WHERE f.cliente_id = @cliente_id AND (@conta_id IS NULL OR f.cloud_conta_id = @conta_id)
ORDER BY f.cloud_conta_id, f.atualizado_em DESC";

    public const string NomesRecursos = @"
SELECT r.chave, r.nome, r.cloud_conta_id FROM recurso r
WHERE r.cliente_id = @cliente_id AND (@conta_id IS NULL OR r.cloud_conta_id = @conta_id) AND r.chave = ANY(@chaves)";

    public const string CaminhosCompartimentos = @"
SELECT c.ocid, c.caminho FROM compartimento c
WHERE c.cliente_id = @cliente_id AND (@conta_id IS NULL OR c.cloud_conta_id = @conta_id) AND c.ocid = ANY(@chaves)";

    /// <summary>Autocomplete: valores da dimensão (TAG sem chave → as chaves de tag, que ficam em chave2).</summary>
    public const string Valores = @"
SELECT DISTINCT CASE WHEN @listar_tags THEN f.chave2 ELSE f.chave END AS v
FROM custo_fato f
WHERE f.cliente_id = @cliente_id AND (@conta_id IS NULL OR f.cloud_conta_id = @conta_id)
  AND f.dimensao = @dimensao
  AND (@tag_chave IS NULL OR f.chave2 = @tag_chave)
  AND (@busca IS NULL OR (CASE WHEN @listar_tags THEN f.chave2 ELSE f.chave END) ILIKE @busca ESCAPE '\')
ORDER BY v
LIMIT 50";
}

public sealed class ExploradorRepositorio(CloudVistaDb db)
{
    public Task<RespostaExplorador> ConsultarAsync(UsuarioAtual u, ConsultaExplorador c, bool podeFator, DateOnly hoje, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            var (antDe, _) = c.Anterior;

            var linhas = new List<LinhaAgregada>();
            await using (var cmd = Comando(conn, tx, ExploradorSql.Consulta, c))
            {
                cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = antDe });
                cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = c.Ate });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    linhas.Add(new LinhaAgregada(r.GetFieldValue<DateOnly>(0), r.GetString(1), "", r.GetString(2).Trim(), r.GetDecimal(3)));
            }

            var creditos = new List<LinhaAgregada>();
            if (c.Granularidade == "MES")
            {
                await using var cmd = Base(conn, tx, ExploradorSql.Creditos, c.ClienteId, c.ContaId);
                cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = c.De });
                cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = c.Ate });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    creditos.Add(new LinhaAgregada(r.GetFieldValue<DateOnly>(0), "", "", r.GetString(1).Trim(), r.GetDecimal(2)));
            }

            // Fator: exatamente a regra da Visão Geral (FatorRegras.Disponivel / Aplicar). CLIENTE nunca.
            var fatores = podeFator
                ? await CustosRepositorio.LerFatoresAsync(conn, tx, CustosSql.ListarFatores, c.ClienteId, null, ct)
                : [];
            var periodos = ExploradorRegras.Periodos(c.Granularidade, c.De, c.Ate);
            var fatorDisponivel = podeFator && FatorRegras.Disponivel(fatores, c.ContaId, hoje, periodos);
            var comFator = c.ComFator && fatorDisponivel;
            if (comFator)
            {
                linhas = FatorRegras.Aplicar(linhas, fatores, c.ContaId).ToList();
                creditos = FatorRegras.Aplicar(creditos, fatores, c.ContaId).ToList();
            }

            var moeda = ExploradorRegras.EscolherMoeda(linhas.Where(l => l.Periodo >= c.De), c.Moeda);
            decimal? totalCreditos = creditos.Count == 0 ? null : creditos.Where(l => l.Moeda == moeda).Sum(l => l.Valor);

            var rotulos = await RotulosAsync(conn, tx, c, linhas.Where(l => l.Periodo >= c.De).Select(l => l.Chave).Distinct().ToArray(), ct);
            var prov = await ProvenienciaAsync(conn, tx, c.ClienteId, c.ContaId, ct);
            return ExploradorRegras.Montar(c, linhas, totalCreditos, rotulos, comFator, fatorDisponivel, prov);
        }, ct);

    public Task<List<string>> ValoresAsync(UsuarioAtual u, long clienteId, long? contaId, string dimensao, string? tagChave, string? busca, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            await using var cmd = Base(conn, tx, ExploradorSql.Valores, clienteId, contaId);
            cmd.Parameters.Add(new NpgsqlParameter("dimensao", NpgsqlDbType.Varchar) { Value = dimensao });
            cmd.Parameters.Add(new NpgsqlParameter("tag_chave", NpgsqlDbType.Text) { Value = (object?)tagChave ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("listar_tags", NpgsqlDbType.Boolean) { Value = dimensao == "TAG" && tagChave is null });
            cmd.Parameters.Add(new NpgsqlParameter("busca", NpgsqlDbType.Text)
                { Value = busca is null ? DBNull.Value : "%" + InventarioRegras.EscaparLike(busca) + "%" });
            var lista = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) if (!r.IsDBNull(0) && r.GetString(0).Length > 0) lista.Add(r.GetString(0));
            return lista;
        }, ct);

    // ── auxiliares ──────────────────────────────────────────────────────────

    /// <summary>RECURSO → nome do inventário (+ conta); COMPARTIMENTO → caminho quando a chave é OCID.</summary>
    private static async Task<Dictionary<string, RotuloChave>> RotulosAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        ConsultaExplorador c, string[] chaves, CancellationToken ct)
    {
        var rotulos = new Dictionary<string, RotuloChave>(StringComparer.Ordinal);
        string? sql = c.Dimensao switch
        {
            "RECURSO" => ExploradorSql.NomesRecursos,
            "COMPARTIMENTO" => ExploradorSql.CaminhosCompartimentos,
            _ => null,
        };
        if (sql is null) return rotulos;
        var consultar = c.Dimensao == "COMPARTIMENTO" ? chaves.Where(k => k.StartsWith("ocid1.", StringComparison.Ordinal)).ToArray() : chaves;
        if (consultar.Length == 0) return rotulos;

        await using var cmd = Base(conn, tx, sql, c.ClienteId, c.ContaId);
        cmd.Parameters.Add(new NpgsqlParameter("chaves", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = consultar });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rotulos[r.GetString(0)] = new RotuloChave(r.GetString(1), c.Dimensao == "RECURSO" ? r.GetInt64(2) : null);
        return rotulos;
    }

    private static async Task<List<ProvenienciaCusto>> ProvenienciaAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, long? contaId, CancellationToken ct)
    {
        var lista = new List<ProvenienciaCusto>();
        await using var cmd = Base(conn, tx, ExploradorSql.Proveniencia, clienteId, contaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new ProvenienciaCusto(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetFieldValue<DateTimeOffset>(2), r.IsDBNull(3) ? null : r.GetInt64(3)));
        return lista;
    }

    private static NpgsqlCommand Base(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, long clienteId, long? contaId)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)contaId ?? DBNull.Value });
        return cmd;
    }

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, ConsultaExplorador c)
    {
        var cmd = Base(conn, tx, sql, c.ClienteId, c.ContaId);
        cmd.Parameters.Add(new NpgsqlParameter("dimensao", NpgsqlDbType.Varchar) { Value = c.Dimensao });
        cmd.Parameters.Add(new NpgsqlParameter("granularidade", NpgsqlDbType.Varchar) { Value = c.Granularidade });
        cmd.Parameters.Add(new NpgsqlParameter("tag_chave", NpgsqlDbType.Text) { Value = (object?)c.TagChave ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("filtro", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = c.FiltroChave.ToArray() });
        return cmd;
    }
}
