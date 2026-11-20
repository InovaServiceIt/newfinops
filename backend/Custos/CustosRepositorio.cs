using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Custos;

/// <summary>SQL da Visão Geral (custo agregado) e do fator do cliente. Parametrizado; RLS por app_cliente_visivel(cliente_id).</summary>
public static class CustosSql
{
    private const string FiltroBase = "cliente_id = @cliente_id AND (@conta_id IS NULL OR cloud_conta_id = @conta_id)";

    public const string Fato = $@"
SELECT periodo, chave, chave2, moeda, sum(valor) AS valor
FROM custo_fato
WHERE {FiltroBase}
  AND dimensao = @dimensao AND granularidade = @granularidade
  AND periodo BETWEEN @de AND @ate
GROUP BY periodo, chave, chave2, moeda";

    public const string Previsao = $@"
SELECT dia, moeda, sum(valor) AS valor
FROM custo_previsao
WHERE {FiltroBase} AND dia BETWEEN @de AND @ate
GROUP BY dia, moeda";

    public const string Credito = $@"
SELECT mes, moeda, sum(valor) AS valor
FROM custo_credito
WHERE {FiltroBase} AND mes BETWEEN @de AND @ate
GROUP BY mes, moeda";

    /// <summary>Último dia com custo real (SERVICO/DIA) no filtro.</summary>
    public const string DadosAte = $@"
SELECT max(periodo) FROM custo_fato
WHERE {FiltroBase} AND dimensao = 'SERVICO' AND granularidade = 'DIA'";

    /// <summary>Proveniência: a importação mais recente que gravou fatos no filtro.</summary>
    public const string UltimaImportacao = $@"
SELECT job_execucao_id, atualizado_em FROM custo_fato
WHERE {FiltroBase}
ORDER BY atualizado_em DESC, job_execucao_id DESC
LIMIT 1";

    private const string ColunasFator = @"f.id, f.cliente_id, c.nome AS cliente_nome, f.cloud_conta_id, cc.nome AS conta_nome,
       f.fator, f.vigente_desde, f.vigente_ate, f.criado_em, f.criado_por::text AS criado_por";

    public const string ListarFatores = $@"
SELECT {ColunasFator}
FROM fator_cliente f
JOIN cliente c ON c.id = f.cliente_id
LEFT JOIN cloud_conta cc ON cc.id = f.cloud_conta_id
WHERE (@cliente_id IS NULL OR f.cliente_id = @cliente_id)
ORDER BY c.nome, cc.nome NULLS FIRST, f.vigente_desde DESC, f.id DESC";

    public const string FatorPorId = $@"
SELECT {ColunasFator}
FROM fator_cliente f
JOIN cliente c ON c.id = f.cliente_id
LEFT JOIN cloud_conta cc ON cc.id = f.cloud_conta_id
WHERE f.id = @id";

    /// <summary>Serializa as alterações de fator do mesmo cliente (checagem de sobreposição sem corrida).</summary>
    public const string TravarCliente = "SELECT pg_advisory_xact_lock(hashtext('cv:fator_cliente'), @cliente_id::int)";

    public const string ContaDoCliente = "SELECT cliente_id FROM cloud_conta WHERE id = @conta_id";

    public const string ClienteExiste = "SELECT 1 FROM cliente WHERE id = @cliente_id";

    public const string CriarFator = @"
INSERT INTO fator_cliente (cliente_id, cloud_conta_id, fator, vigente_desde, vigente_ate, criado_por)
VALUES (@cliente_id, @conta_id, @fator, @desde, @ate, @criado_por)
RETURNING id";

    public const string AtualizarFator = @"
UPDATE fator_cliente
SET cloud_conta_id = @conta_id, fator = @fator, vigente_desde = @desde, vigente_ate = @ate
WHERE id = @id";

    public const string ClienteDoFator = "SELECT cliente_id FROM fator_cliente WHERE id = @id FOR UPDATE";
}

public sealed class CustosRepositorio
{
    private readonly CloudVistaDb _db;

    public CustosRepositorio(CloudVistaDb db) => _db = db;

    public Task<DadosVisaoGeral> CarregarAsync(UsuarioAtual usuario, long clienteId, long? contaId, DateOnly hoje, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            var mesAtual = VisaoGeralCustos.PrimeiroDoMes(hoje);
            var fimMes = mesAtual.AddMonths(1).AddDays(-1);

            Task<List<LinhaAgregada>> Fato(string dimensao, string granularidade, DateOnly de, DateOnly ate) =>
                LerFatoAsync(conn, tx, clienteId, contaId, dimensao, granularidade, de, ate, ct);

            // Mesma conexão/transação: as consultas rodam em sequência.
            var servicoMes = await Fato("SERVICO", "MES", VisaoGeralCustos.InicioDozeMeses(hoje), mesAtual);
            var servicoDia = await Fato("SERVICO", "DIA", VisaoGeralCustos.InicioDiario(hoje), hoje);
            var compMes = await Fato("COMPARTIMENTO", "MES", mesAtual, mesAtual);
            var recursoMes = await Fato("RECURSO", "MES", mesAtual, mesAtual);
            var previsao = await LerSimplesAsync(conn, tx, CustosSql.Previsao, clienteId, contaId, mesAtual, fimMes, ct);
            var credito = await LerSimplesAsync(conn, tx, CustosSql.Credito, clienteId, contaId, mesAtual, mesAtual, ct);

            DateOnly? dadosAte;
            await using (var cmd = Comando(conn, tx, CustosSql.DadosAte, clienteId, contaId))
                dadosAte = await cmd.ExecuteScalarAsync(ct) is DateTime dt ? DateOnly.FromDateTime(dt)
                    : null;

            long? ultimoJob = null;
            DateTimeOffset? ultimaImportacao = null;
            await using (var cmd = Comando(conn, tx, CustosSql.UltimaImportacao, clienteId, contaId))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                if (await r.ReadAsync(ct))
                {
                    ultimoJob = r.IsDBNull(0) ? null : r.GetInt64(0);
                    ultimaImportacao = r.IsDBNull(1) ? null : r.GetFieldValue<DateTimeOffset>(1);
                }
            }

            var fatores = await LerFatoresAsync(conn, tx, CustosSql.ListarFatores, clienteId, null, ct);

            return new DadosVisaoGeral(servicoMes, servicoDia, compMes, recursoMes, previsao, credito,
                dadosAte, ultimoJob, ultimaImportacao, fatores);
        }, ct);

    public Task<List<FatorCliente>> ListarFatoresAsync(UsuarioAtual usuario, long? clienteId, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, (conn, tx) => LerFatoresAsync(conn, tx, CustosSql.ListarFatores, clienteId, null, ct), ct);

    public Task<FatorCliente> CriarFatorAsync(UsuarioAtual ator, FatorEntrada entrada, CancellationToken ct)
    {
        var v = FatorRegras.Validar(entrada);
        return _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            await PrepararAsync(conn, tx, v.ClienteId, v.ContaId, null, v.Desde, v.Ate, ct);

            long id;
            await using (var cmd = new NpgsqlCommand(CustosSql.CriarFator, conn, tx))
            {
                ParametrosFator(cmd, v.ClienteId, v.ContaId, v.Fator, v.Desde, v.Ate);
                cmd.Parameters.Add(new NpgsqlParameter("criado_por", NpgsqlDbType.Varchar) { Value = ator.Email });
                id = (long)(await cmd.ExecuteScalarAsync(ct))!;
            }
            return await CarregarFatorAsync(conn, tx, id, ct);
        }, ct);
    }

    /// <summary>O cliente do fator não muda; conta, fator e vigência sim.</summary>
    public Task<FatorCliente> AtualizarFatorAsync(UsuarioAtual ator, long id, FatorEntrada entrada, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            long clienteAtual;
            await using (var cmd = new NpgsqlCommand(CustosSql.ClienteDoFator, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                clienteAtual = await cmd.ExecuteScalarAsync(ct) is long c ? c
                    : throw ErroNegocioException.NaoEncontrado("Fator não encontrado.");
            }
            if (entrada.ClienteId is { } novo && novo != clienteAtual)
                throw ErroNegocioException.Validacao("Cliente: não pode ser alterado; cadastre um novo fator.");

            var v = FatorRegras.Validar(entrada with { ClienteId = clienteAtual });
            await PrepararAsync(conn, tx, v.ClienteId, v.ContaId, id, v.Desde, v.Ate, ct);

            await using (var cmd = new NpgsqlCommand(CustosSql.AtualizarFator, conn, tx))
            {
                ParametrosFator(cmd, v.ClienteId, v.ContaId, v.Fator, v.Desde, v.Ate);
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return await CarregarFatorAsync(conn, tx, id, ct);
        }, ct);

    // ── auxiliares ──────────────────────────────────────────────────────────

    /// <summary>Trava o cliente, confere cliente/conta e rejeita vigência sobreposta.</summary>
    private static async Task PrepararAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, long? contaId,
        long? ignorarId, DateOnly desde, DateOnly? ate, CancellationToken ct)
    {
        await using (var cmd = new NpgsqlCommand(CustosSql.TravarCliente, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(CustosSql.ClienteExiste, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
            if (await cmd.ExecuteScalarAsync(ct) is null)
                throw ErroNegocioException.Validacao("Cliente não encontrado.");
        }
        if (contaId is { } conta)
        {
            await using var cmd = new NpgsqlCommand(CustosSql.ContaDoCliente, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = conta });
            if (await cmd.ExecuteScalarAsync(ct) is not long dono || dono != clienteId)
                throw ErroNegocioException.Validacao("Conta: selecione uma conta deste cliente.");
        }

        var existentes = await LerFatoresAsync(conn, tx, CustosSql.ListarFatores, clienteId, null, ct);
        if (FatorRegras.Sobrepoe(existentes, ignorarId, contaId, desde, ate))
            throw ErroNegocioException.Conflito("Já existe um fator com vigência sobreposta para este cliente/conta.");
    }

    private static void ParametrosFator(NpgsqlCommand cmd, long clienteId, long? contaId, decimal fator, DateOnly desde, DateOnly? ate)
    {
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)contaId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("fator", NpgsqlDbType.Numeric) { Value = fator });
        cmd.Parameters.Add(new NpgsqlParameter("desde", NpgsqlDbType.Date) { Value = desde });
        cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = (object?)ate ?? DBNull.Value });
    }

    private static async Task<FatorCliente> CarregarFatorAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id, CancellationToken ct) =>
        (await LerFatoresAsync(conn, tx, CustosSql.FatorPorId, null, id, ct)).SingleOrDefault()
        ?? throw ErroNegocioException.NaoEncontrado("Fator não encontrado.");

    private static async Task<List<FatorCliente>> LerFatoresAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql,
        long? clienteId, long? id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        if (sql.Contains("@cliente_id"))
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = (object?)clienteId ?? DBNull.Value });
        if (sql.Contains("@id"))
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = (object?)id ?? DBNull.Value });

        var lista = new List<FatorCliente>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new FatorCliente(
                r.GetInt64(0), r.GetInt64(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3), r.IsDBNull(4) ? null : r.GetString(4),
                r.GetDecimal(5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7),
                r.GetFieldValue<DateTimeOffset>(8), r.IsDBNull(9) ? null : r.GetString(9)));
        }
        return lista;
    }

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, long clienteId, long? contaId)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)contaId ?? DBNull.Value });
        return cmd;
    }

    private static async Task<List<LinhaAgregada>> LerFatoAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, long? contaId,
        string dimensao, string granularidade, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = Comando(conn, tx, CustosSql.Fato, clienteId, contaId);
        cmd.Parameters.Add(new NpgsqlParameter("dimensao", NpgsqlDbType.Varchar) { Value = dimensao });
        cmd.Parameters.Add(new NpgsqlParameter("granularidade", NpgsqlDbType.Varchar) { Value = granularidade });
        cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = de });
        cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = ate });

        var lista = new List<LinhaAgregada>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new LinhaAgregada(r.GetFieldValue<DateOnly>(0), r.GetString(1), r.GetString(2), r.GetString(3).Trim(), r.GetDecimal(4)));
        return lista;
    }

    private static async Task<List<LinhaAgregada>> LerSimplesAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql,
        long clienteId, long? contaId, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = Comando(conn, tx, sql, clienteId, contaId);
        cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = de });
        cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = ate });

        var lista = new List<LinhaAgregada>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new LinhaAgregada(r.GetFieldValue<DateOnly>(0), "", "", r.GetString(1).Trim(), r.GetDecimal(2)));
        return lista;
    }
}
