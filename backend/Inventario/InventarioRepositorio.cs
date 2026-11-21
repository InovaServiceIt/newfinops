using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Inventario;

/// <summary>SQL do inventário (parametrizado; sinais e ordenação vêm de listas constantes). RLS por cliente.</summary>
public static class InventarioSql
{
    public const string Base = "r.cliente_id = @cliente_id AND (@conta_id IS NULL OR r.cloud_conta_id = @conta_id)";

    /// <summary>Filtro da tabela (sem o sinal, que é concatenado de lista constante).</summary>
    public const string Filtro = Base + @"
  AND (@removidos OR r.removido_em IS NULL)
  AND (@regiao IS NULL OR r.regiao = @regiao)
  AND (@tipo IS NULL OR r.tipo = @tipo)
  AND (@estado IS NULL OR r.estado = @estado)
  AND (@compartimento IS NULL OR c.caminho = @compartimento OR c.caminho LIKE @compartimento_prefixo ESCAPE '\')
  AND (@busca IS NULL OR r.nome ILIKE @busca ESCAPE '\' OR r.chave ILIKE @busca ESCAPE '\')
  AND (@tag_defined IS NULL AND @tag_freeform IS NULL OR r.tags @> CAST(@tag_defined AS jsonb) OR r.tags @> CAST(@tag_freeform AS jsonb))
  AND (@tag_valor IS NULL OR jsonb_path_exists(r.tags, '$.** ? (@ == $v)', jsonb_build_object('v', @tag_valor)))";

    public const string De = @"
FROM recurso r
LEFT JOIN compartimento c ON c.cloud_conta_id = r.cloud_conta_id AND c.ocid = r.compartimento_ocid";

    public static string Where(string? sinal) =>
        " WHERE " + Filtro + (sinal is null ? "" : " AND (" + InventarioRegras.Sinais[sinal] + ")");

    public static string Contar(string? sinal) => "SELECT count(*)" + De + Where(sinal);

    public static string Listar(string? sinal, string ordem, bool desc) => @"
SELECT r.cloud_conta_id, r.chave, r.nome, r.tipo, r.regiao, c.caminho, r.estado, r.criado_em_cloud, r.tags::text,
       r.ultimo_visto_em, r.removido_em" + De + Where(sinal) + $@"
ORDER BY {ordem} {(desc ? "DESC" : "ASC")} NULLS LAST, r.chave
LIMIT @limite OFFSET @deslocamento";

    public static string Exportar(string? sinal) => @"
SELECT cc.nome, r.nome, r.chave, r.tipo, r.regiao, c.caminho, r.estado, r.criado_em_cloud, r.ultimo_visto_em,
       r.removido_em, r.tags::text" + De + @"
JOIN cloud_conta cc ON cc.id = r.cloud_conta_id" + Where(sinal) + @"
ORDER BY cc.nome, r.tipo, lower(r.nome), r.chave
LIMIT @limite";

    /// <summary>KPIs + sinais numa ida (só ativos).</summary>
    public static readonly string Kpis = "SELECT count(*) AS total, " +
        "count(*) FILTER (WHERE r.primeiro_visto_em >= now() - interval '7 days') AS novos, " +
        string.Join(", ", InventarioRegras.Sinais.Select(s => $"count(*) FILTER (WHERE {s.Value}) AS {s.Key}")) +
        " FROM recurso r WHERE " + Base + " AND r.removido_em IS NULL";

    public const string Removidos7d = "SELECT count(*) FROM recurso r WHERE " + Base +
        " AND r.removido_em >= now() - interval '7 days'";

    public const string PorTipo = "SELECT r.tipo, count(*) FROM recurso r WHERE " + Base +
        " AND r.removido_em IS NULL GROUP BY r.tipo ORDER BY r.tipo";

    public const string Regioes = "SELECT DISTINCT r.regiao FROM recurso r WHERE " + Base + " ORDER BY r.regiao";

    public const string Estados = "SELECT DISTINCT r.estado FROM recurso r WHERE " + Base + " AND r.estado IS NOT NULL ORDER BY r.estado";

    public const string Compartimentos = @"
SELECT c.ocid, c.caminho FROM compartimento c
WHERE c.cliente_id = @cliente_id AND (@conta_id IS NULL OR c.cloud_conta_id = @conta_id) AND c.removido_em IS NULL
ORDER BY c.caminho
LIMIT 2000";

    public const string Proveniencia = @"
SELECT DISTINCT ON (r.cloud_conta_id) r.cloud_conta_id, cc.nome, r.ultimo_visto_em, r.job_execucao_id
FROM recurso r
JOIN cloud_conta cc ON cc.id = r.cloud_conta_id
WHERE " + Base + @"
ORDER BY r.cloud_conta_id, r.ultimo_visto_em DESC";

    public const string Detalhe = @"
SELECT r.cloud_conta_id, cc.nome, r.chave, r.nome, r.tipo, r.regiao, r.compartimento_ocid, c.caminho, r.estado,
       r.criado_em_cloud, r.tags::text, r.atributos::text, r.primeiro_visto_em, r.ultimo_visto_em, r.removido_em,
       r.job_execucao_id
FROM recurso r
JOIN cloud_conta cc ON cc.id = r.cloud_conta_id
LEFT JOIN compartimento c ON c.cloud_conta_id = r.cloud_conta_id AND c.ocid = r.compartimento_ocid
WHERE r.cloud_conta_id = @conta_id AND r.chave = @chave";

    /// <summary>Nomes do inventário para a Visão Geral (top recursos por OCID).</summary>
    public const string NomesRecursos = @"
SELECT r.chave, r.nome FROM recurso r
WHERE r.cliente_id = @cliente_id AND (@conta_id IS NULL OR r.cloud_conta_id = @conta_id) AND r.chave = ANY(@chaves)";
}

public sealed class InventarioRepositorio(CloudVistaDb db)
{
    public Task<ResumoInventarioSaida> ResumoAsync(UsuarioAtual u, long clienteId, long? contaId, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            NpgsqlCommand Cmd(string sql) => Comando(conn, tx, sql, clienteId, contaId);

            long total = 0, novos = 0;
            var sinais = new Dictionary<string, long>();
            await using (var cmd = Cmd(InventarioSql.Kpis))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                if (await r.ReadAsync(ct))
                {
                    total = r.GetInt64(0);
                    novos = r.GetInt64(1);
                    var i = 2;
                    foreach (var chave in InventarioRegras.Sinais.Keys) sinais[chave] = r.GetInt64(i++);
                }
            }

            long removidos;
            await using (var cmd = Cmd(InventarioSql.Removidos7d))
                removidos = (long)(await cmd.ExecuteScalarAsync(ct))!;

            var tipos = new List<ContagemItem>();
            await using (var cmd = Cmd(InventarioSql.PorTipo))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) tipos.Add(new ContagemItem(r.GetString(0), r.GetInt64(1)));

            var porCategoria = tipos.GroupBy(t => t.Chave.Split('.')[0])
                .Select(g => new ContagemItem(g.Key, g.Sum(t => t.Total))).OrderByDescending(c => c.Total).ToList();

            var regioes = await TextosAsync(Cmd(InventarioSql.Regioes), ct);
            var estados = await TextosAsync(Cmd(InventarioSql.Estados), ct);

            var comps = new List<CompartimentoOpcao>();
            await using (var cmd = Cmd(InventarioSql.Compartimentos))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) comps.Add(new CompartimentoOpcao(r.GetString(0), r.GetString(1)));

            var prov = new List<ProvenienciaConta>();
            await using (var cmd = Cmd(InventarioSql.Proveniencia))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    prov.Add(new ProvenienciaConta(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetFieldValue<DateTimeOffset>(2), r.IsDBNull(3) ? null : r.GetInt64(3)));

            return new ResumoInventarioSaida(total, novos, removidos, porCategoria, sinais, regioes, tipos, estados, comps, prov);
        }, ct);

    public Task<PaginaRecursos> ListarAsync(UsuarioAtual u, FiltroInventario f, string ordem, bool desc, int pagina, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            long total;
            await using (var cmd = ComandoFiltro(conn, tx, InventarioSql.Contar(f.Sinal), f))
                total = (long)(await cmd.ExecuteScalarAsync(ct))!;

            var itens = new List<RecursoItem>();
            await using (var cmd = ComandoFiltro(conn, tx, InventarioSql.Listar(f.Sinal, ordem, desc), f))
            {
                cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = InventarioRegras.TamanhoPagina });
                cmd.Parameters.Add(new NpgsqlParameter("deslocamento", NpgsqlDbType.Integer) { Value = (pagina - 1) * InventarioRegras.TamanhoPagina });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    itens.Add(new RecursoItem(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                        Txt(r, 5), Txt(r, 6), Data(r, 7), Json(r, 8), r.GetFieldValue<DateTimeOffset>(9), Data(r, 10)));
            }
            return new PaginaRecursos(itens, total, pagina, InventarioRegras.TamanhoPagina);
        }, ct);

    public Task<RecursoDetalhe> DetalheAsync(UsuarioAtual u, long contaId, string chave, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(InventarioSql.Detalhe, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
            cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = chave });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw ErroNegocioException.NaoEncontrado("Recurso não encontrado.");
            return new RecursoDetalhe(r.GetInt64(0), Txt(r, 1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5),
                Txt(r, 6), Txt(r, 7), Txt(r, 8), Data(r, 9), Json(r, 10), Json(r, 11), r.GetFieldValue<DateTimeOffset>(12),
                r.GetFieldValue<DateTimeOffset>(13), Data(r, 14), r.IsDBNull(15) ? null : r.GetInt64(15));
        }, ct);

    /// <summary>Linhas do CSV já sanitizadas (até LimiteCsv).</summary>
    public Task<List<string>> ExportarAsync(UsuarioAtual u, FiltroInventario f, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            var linhas = new List<string> { InventarioRegras.LinhaCsv(InventarioRegras.CabecalhoCsv) };
            await using var cmd = ComandoFiltro(conn, tx, InventarioSql.Exportar(f.Sinal), f);
            cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = InventarioRegras.LimiteCsv });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                linhas.Add(InventarioRegras.LinhaCsv([
                    Txt(r, 0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Txt(r, 5), Txt(r, 6),
                    InventarioRegras.Data(Data(r, 7)), InventarioRegras.Data(r.GetFieldValue<DateTimeOffset>(8)),
                    InventarioRegras.Data(Data(r, 9)), Txt(r, 10)]));
            return linhas;
        }, ct);

    // ── auxiliares ──────────────────────────────────────────────────────────

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, long clienteId, long? contaId)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)contaId ?? DBNull.Value });
        return cmd;
    }

    private static NpgsqlCommand ComandoFiltro(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, FiltroInventario f)
    {
        var cmd = Comando(conn, tx, sql, f.ClienteId, f.ContaId);
        void Texto(string nome, string? valor) =>
            cmd.Parameters.Add(new NpgsqlParameter(nome, NpgsqlDbType.Text) { Value = (object?)valor ?? DBNull.Value });

        cmd.Parameters.Add(new NpgsqlParameter("removidos", NpgsqlDbType.Boolean) { Value = f.Removidos });
        Texto("regiao", f.Regiao);
        Texto("tipo", f.Tipo);
        Texto("estado", f.Estado);
        Texto("compartimento", f.Compartimento);
        Texto("compartimento_prefixo", f.Compartimento is null ? null : InventarioRegras.EscaparLike(f.Compartimento) + "/%");
        Texto("busca", f.Busca is null ? null : "%" + InventarioRegras.EscaparLike(f.Busca) + "%");
        var tag = InventarioRegras.Tag(f.Tag);
        Texto("tag_defined", tag?.JsonDefined);
        Texto("tag_freeform", tag?.JsonFreeform);
        Texto("tag_valor", tag?.Valor);
        return cmd;
    }

    private static async Task<List<string>> TextosAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using (cmd)
        {
            var lista = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) if (!r.IsDBNull(0)) lista.Add(r.GetString(0));
            return lista;
        }
    }

    private static string? Txt(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static DateTimeOffset? Data(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetFieldValue<DateTimeOffset>(i);

    private static JsonElement Json(NpgsqlDataReader r, int i)
    {
        using var doc = JsonDocument.Parse(r.IsDBNull(i) ? "{}" : r.GetString(i));
        return doc.RootElement.Clone();
    }
}
