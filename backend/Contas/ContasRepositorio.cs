using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Contas;

/// <summary>SQL das contas de cloud (parametrizado, sem SELECT *). RLS de cloud_conta filtra por perfil.</summary>
public static class ContasSql
{
    private const string Colunas = @"cc.id, cc.cliente_id, c.nome AS cliente_nome, cc.provedor, cc.nome, cc.identificador,
       cc.escopo, cc.regiao_padrao, cc.ativo, cc.criado_em, cr.status AS credencial_status";

    public const string Listar = $@"
SELECT {Colunas}
FROM cloud_conta cc
JOIN cliente c ON c.id = cc.cliente_id
LEFT JOIN credencial cr ON cr.cloud_conta_id = cc.id
WHERE (@cliente_id IS NULL OR cc.cliente_id = @cliente_id)
  AND (@provedor IS NULL OR cc.provedor = @provedor)
  AND (@incluir_inativas OR cc.ativo)
ORDER BY c.nome, cc.provedor, cc.nome";

    public const string PorId = $@"
SELECT {Colunas}
FROM cloud_conta cc
JOIN cliente c ON c.id = cc.cliente_id
LEFT JOIN credencial cr ON cr.cloud_conta_id = cc.id
WHERE cc.id = @id";

    public const string ClienteAtivo = "SELECT ativo FROM cliente WHERE id = @cliente_id";

    public const string Criar = @"
INSERT INTO cloud_conta (cliente_id, provedor, nome, identificador, escopo, regiao_padrao, ativo)
VALUES (@cliente_id, @provedor, @nome, @identificador, @escopo, @regiao_padrao, true)
RETURNING id";

    public const string ParaAlterar = "SELECT cliente_id, provedor, identificador FROM cloud_conta WHERE id = @id FOR UPDATE";

    public const string Atualizar = "UPDATE cloud_conta SET nome = @nome, escopo = @escopo, regiao_padrao = @regiao_padrao WHERE id = @id";

    public const string AlterarAtivo = "UPDATE cloud_conta SET ativo = @ativo WHERE id = @id";
}

public sealed class ContasRepositorio
{
    private const string MensagemDuplicada = "Esta conta já está cadastrada (provedor + identificador).";

    private readonly CloudVistaDb _db;

    public ContasRepositorio(CloudVistaDb db) => _db = db;

    /// <summary>Lista no contexto RLS do usuário (admin = todas; demais = dos clientes vinculados).</summary>
    public Task<List<ContaSaida>> ListarAsync(UsuarioAtual usuario, long? clienteId, string? provedor, bool incluirInativas, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(ContasSql.Listar, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = (object?)clienteId ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("provedor", NpgsqlDbType.Varchar) { Value = (object?)provedor ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("incluir_inativas", NpgsqlDbType.Boolean) { Value = incluirInativas });
            return await LerAsync(cmd, ct);
        }, ct);

    public async Task<ContaSaida> CriarAsync(UsuarioAtual ator, ContaValida c, CancellationToken ct)
    {
        try
        {
            return await _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
            {
                await GarantirClienteAtivoAsync(conn, tx, c.ClienteId, ct);

                long id;
                await using (var cmd = new NpgsqlCommand(ContasSql.Criar, conn, tx))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = c.ClienteId });
                    cmd.Parameters.Add(new NpgsqlParameter("provedor", NpgsqlDbType.Varchar) { Value = c.Provedor });
                    cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = c.Nome });
                    cmd.Parameters.Add(new NpgsqlParameter("identificador", NpgsqlDbType.Varchar) { Value = c.Identificador });
                    cmd.Parameters.Add(new NpgsqlParameter("escopo", NpgsqlDbType.Varchar) { Value = c.Escopo });
                    cmd.Parameters.Add(new NpgsqlParameter("regiao_padrao", NpgsqlDbType.Varchar) { Value = (object?)c.RegiaoPadrao ?? DBNull.Value });
                    id = (long)(await cmd.ExecuteScalarAsync(ct))!;
                }
                return await CarregarAsync(conn, tx, id, ct);
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Não revela de qual cliente é a conta já cadastrada.
            throw ErroNegocioException.Conflito(MensagemDuplicada);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw ErroNegocioException.Validacao("Dados da conta inválidos para o provedor/escopo.");
        }
    }

    public async Task<ContaSaida> AtualizarAsync(UsuarioAtual ator, long id, ContaAlteracao entrada, CancellationToken ct)
    {
        try
        {
            return await _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
            {
                var (_, provedor, identificador) = await CarregarParaAlterarAsync(conn, tx, id, ct);
                var dados = ContaRegras.ValidarAlteracao(provedor, identificador, entrada);

                await using (var cmd = new NpgsqlCommand(ContasSql.Atualizar, conn, tx))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = dados.Nome });
                    cmd.Parameters.Add(new NpgsqlParameter("escopo", NpgsqlDbType.Varchar) { Value = dados.Escopo });
                    cmd.Parameters.Add(new NpgsqlParameter("regiao_padrao", NpgsqlDbType.Varchar) { Value = (object?)dados.RegiaoPadrao ?? DBNull.Value });
                    cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                return await CarregarAsync(conn, tx, id, ct);
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw ErroNegocioException.Validacao("Dados da conta inválidos para o provedor/escopo.");
        }
    }

    /// <summary>Ativar exige cliente ativo (mesma regra da criação); desativar é sempre permitido.</summary>
    public Task<ContaSaida> AlterarAtivoAsync(UsuarioAtual ator, long id, bool ativo, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            var (clienteId, _, _) = await CarregarParaAlterarAsync(conn, tx, id, ct);
            if (ativo)
                await GarantirClienteAtivoAsync(conn, tx, clienteId, ct);

            await using (var cmd = new NpgsqlCommand(ContasSql.AlterarAtivo, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("ativo", NpgsqlDbType.Boolean) { Value = ativo });
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return await CarregarAsync(conn, tx, id, ct);
        }, ct);

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static async Task GarantirClienteAtivoAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(ContasSql.ClienteAtivo, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        var ativo = await cmd.ExecuteScalarAsync(ct);
        if (ativo is not true)
            throw ErroNegocioException.Validacao("Cliente: inexistente ou inativo. Contas só podem ser cadastradas/ativadas para clientes ativos.");
    }

    private static async Task<(long ClienteId, string Provedor, string Identificador)> CarregarParaAlterarAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, long id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(ContasSql.ParaAlterar, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw ErroNegocioException.NaoEncontrado("Conta não encontrada.");
        return (r.GetInt64(0), r.GetString(1), r.GetString(2));
    }

    private static async Task<ContaSaida> CarregarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(ContasSql.PorId, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        return (await LerAsync(cmd, ct)).FirstOrDefault() ?? throw ErroNegocioException.NaoEncontrado("Conta não encontrada.");
    }

    private static async Task<List<ContaSaida>> LerAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<ContaSaida>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new ContaSaida(
                r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5),
                r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetBoolean(8),
                r.GetFieldValue<DateTimeOffset>(9), r.IsDBNull(10) ? null : r.GetString(10)));
        }
        return lista;
    }
}
