using System.Security.Cryptography;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Cofre;

// ── Contratos ────────────────────────────────────────────────────────────────

public sealed record CredencialEntrada(string? Tipo, Dictionary<string, string?>? Campos, bool? AceiteSomenteLeitura);
public sealed record AtivoCredencialEntrada(bool Ativo);

public sealed record CredencialMetadados(
    bool Existe, string? Tipo, string? Dica, string? Status, DateTimeOffset? TestadaEm,
    string? TesteResultado, string? CadastradaPorNome, DateTimeOffset? CadastradaEm)
{
    public static readonly CredencialMetadados Inexistente = new(false, null, null, null, null, null, null, null);
}

public sealed record AuditoriaItem(string Acao, string? Detalhe, string Usuario, string Origem, DateTimeOffset OcorridoEm);

public static class StatusCredencial
{
    public const string PendenteTeste = "PENDENTE_TESTE";
    public const string Ativa = "ATIVA";
    public const string FalhaTeste = "FALHA_TESTE";
    public const string Desativada = "DESATIVADA";
}

/// <summary>
/// SQL do cofre. NUNCA selecionar/retornar credencial.conteudo_cifrado: o svc_cv_api não tem SELECT
/// nessa coluna (proposital). O upsert usa parâmetros no SET (sem EXCLUDED.*), para não ler a coluna.
/// </summary>
public static class CofreSql
{
    public const string Conta = "SELECT cc.cliente_id, cc.provedor, cc.identificador FROM cloud_conta cc WHERE cc.id = @conta_id";

    public const string Metadados = @"
SELECT cr.id, cr.tipo, cr.dica, cr.status, cr.testada_em, cr.teste_resultado, u.nome, cr.cadastrada_em
FROM credencial cr
LEFT JOIN usuario u ON u.id = cr.cadastrada_por
WHERE cr.cloud_conta_id = @conta_id";

    /// <summary>Serializa a criação da DEK do cliente (evita duas DEKs ativas em gravações simultâneas).</summary>
    public const string TravarDekCliente = "SELECT pg_advisory_xact_lock(@namespace, @cliente_id)";

    public const string DekAtiva = "SELECT id, versao_kek, dek_cifrada FROM chave_dados WHERE cliente_id = @cliente_id AND ativa";

    public const string InserirDek = @"
INSERT INTO chave_dados (cliente_id, versao_kek, dek_cifrada, ativa)
VALUES (@cliente_id, @versao_kek, @dek_cifrada, true)
RETURNING id";

    public const string CredencialId = "SELECT id, status FROM credencial WHERE cloud_conta_id = @conta_id";

    public const string Upsert = @"
INSERT INTO credencial (cloud_conta_id, chave_dados_id, tipo, versao_formato, conteudo_cifrado, dica, status, cadastrada_por)
VALUES (@conta_id, @chave_dados_id, @tipo, @versao_formato, @conteudo, @dica, @status, @usuario_id)
ON CONFLICT (cloud_conta_id) DO UPDATE SET
    chave_dados_id   = @chave_dados_id,
    tipo             = @tipo,
    versao_formato   = @versao_formato,
    conteudo_cifrado = @conteudo,
    dica             = @dica,
    status           = @status,
    testada_em       = NULL,
    teste_resultado  = NULL,
    cadastrada_por   = @usuario_id,
    cadastrada_em    = now()
RETURNING id";

    public const string AlterarStatus = @"
UPDATE credencial SET status = @status, testada_em = NULL, teste_resultado = NULL
WHERE cloud_conta_id = @conta_id";

    public const string InserirAuditoria = @"
INSERT INTO credencial_auditoria (cloud_conta_id, credencial_id, acao, detalhe, usuario_id, origem)
VALUES (@conta_id, @credencial_id, @acao, @detalhe, @usuario_id, 'API')";

    /// <summary>
    /// Enfileira o teste da credencial. ON CONFLICT DO NOTHING: o índice único parcial (conta, tipo)
    /// em PENDENTE/EXECUTANDO impede dois testes em andamento → sem linha retornada = já existe.
    /// </summary>
    public const string EnfileirarTeste = @"
INSERT INTO job_execucao (tipo_job, cliente_id, cloud_conta_id, origem, solicitado_por, prioridade)
VALUES ('TESTE_CREDENCIAL', @cliente_id, @conta_id, 'MANUAL', @usuario_id, 10)
ON CONFLICT DO NOTHING
RETURNING id";

    public const string Auditoria = @"
SELECT a.acao, a.detalhe, u.nome, a.origem, a.ocorrido_em
FROM credencial_auditoria a
LEFT JOIN usuario u ON u.id = a.usuario_id
WHERE a.cloud_conta_id = @conta_id
ORDER BY a.ocorrido_em DESC
LIMIT 200";
}

/// <summary>
/// Cofre de credenciais (cifragem em envelope). A API cifra e grava; quem decifra é o coletor.
/// Segredos e texto claro nunca vão para log, exceção ou resposta; buffers são zerados após o uso.
/// </summary>
public sealed class CofreServico
{
    /// <summary>Namespace do advisory lock da DEK ("CVDK").</summary>
    private const int NamespaceTravaDek = 0x4356444B;
    private const short VersaoFormato = CofreCripto.VersaoFormato;

    private readonly CloudVistaDb _db;
    private readonly ChavesMestras _chaves;

    public CofreServico(CloudVistaDb db, ChavesMestras chaves)
    {
        _db = db;
        _chaves = chaves;
    }

    public bool Configurado => _chaves.Configurado;

    public Task<CredencialMetadados> MetadadosAsync(UsuarioAtual usuario, long contaId, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            await ContaAsync(conn, tx, contaId, ct);
            return await LerMetadadosAsync(conn, tx, contaId, ct);
        }, ct);

    public async Task<CredencialMetadados> SalvarAsync(UsuarioAtual usuario, long contaId, CredencialEntrada entrada, CancellationToken ct)
    {
        if (entrada.AceiteSomenteLeitura != true)
            throw ErroNegocioException.Validacao("Confirme que a credencial tem somente permissões de leitura.");
        if (!_chaves.Configurado)
            throw new CofreNaoConfiguradoException();

        return await _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            var (clienteId, provedor, identificador) = await ContaAsync(conn, tx, contaId, ct);
            var credencial = CredencialRegras.Validar(entrada.Tipo, provedor, identificador, entrada.Campos);

            byte[]? dek = null;
            try
            {
                long dekId;
                (dekId, dek) = await ObterOuCriarDekAsync(conn, tx, clienteId, ct);
                var conteudo = CofreCripto.Cifrar(dek, credencial.ConteudoJson, CofreCripto.AadCredencial(contaId, credencial.Tipo));

                var existente = await CredencialIdAsync(conn, tx, contaId, ct);

                long credencialId;
                await using (var cmd = new NpgsqlCommand(CofreSql.Upsert, conn, tx))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                    cmd.Parameters.Add(new NpgsqlParameter("chave_dados_id", NpgsqlDbType.Bigint) { Value = dekId });
                    cmd.Parameters.Add(new NpgsqlParameter("tipo", NpgsqlDbType.Varchar) { Value = credencial.Tipo });
                    cmd.Parameters.Add(new NpgsqlParameter("versao_formato", NpgsqlDbType.Smallint) { Value = VersaoFormato });
                    cmd.Parameters.Add(new NpgsqlParameter("conteudo", NpgsqlDbType.Bytea) { Value = conteudo });
                    cmd.Parameters.Add(new NpgsqlParameter("dica", NpgsqlDbType.Varchar) { Value = credencial.Dica });
                    cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = StatusCredencial.PendenteTeste });
                    cmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = usuario.Id });
                    credencialId = (long)(await cmd.ExecuteScalarAsync(ct))!;
                }

                await AuditarAsync(conn, tx, contaId, credencialId, existente is null ? "CADASTRADA" : "SUBSTITUIDA",
                    "aceite de credencial somente leitura confirmado", usuario.Id, ct);

                return await LerMetadadosAsync(conn, tx, contaId, ct);
            }
            finally
            {
                if (dek is not null) CryptographicOperations.ZeroMemory(dek);
                CryptographicOperations.ZeroMemory(credencial.ConteudoJson);
            }
        }, ct);
    }

    /// <summary>ADMINISTRADOR: DESATIVADA ↔ PENDENTE_TESTE, com auditoria.</summary>
    public Task<CredencialMetadados> AlterarAtivoAsync(UsuarioAtual admin, long contaId, bool ativo, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(admin, async (conn, tx) =>
        {
            await ContaAsync(conn, tx, contaId, ct);
            var existente = await CredencialIdAsync(conn, tx, contaId, ct)
                ?? throw ErroNegocioException.NaoEncontrado("Esta conta não tem credencial cadastrada.");

            var novoStatus = ativo ? StatusCredencial.PendenteTeste : StatusCredencial.Desativada;
            await using (var cmd = new NpgsqlCommand(CofreSql.AlterarStatus, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = novoStatus });
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await AuditarAsync(conn, tx, contaId, existente.Id, ativo ? "REATIVADA" : "DESATIVADA", null, admin.Id, ct);
            return await LerMetadadosAsync(conn, tx, contaId, ct);
        }, ct);

    /// <summary>
    /// Enfileira um job TESTE_CREDENCIAL (MANUAL, prioridade 10) + auditoria TESTE_SOLICITADO.
    /// Retorna o id do job. Teste já em andamento → 409.
    /// </summary>
    public Task<long> SolicitarTesteAsync(UsuarioAtual usuario, long contaId, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            var (clienteId, _, _) = await ContaAsync(conn, tx, contaId, ct);
            var existente = await CredencialIdAsync(conn, tx, contaId, ct)
                ?? throw ErroNegocioException.Conflito("Cadastre uma credencial antes de testar a conexão.");
            if (existente.Status == StatusCredencial.Desativada)
                throw ErroNegocioException.Conflito("A credencial está desativada. Reative-a antes de testar.");

            long jobId;
            await using (var cmd = new NpgsqlCommand(CofreSql.EnfileirarTeste, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                cmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = usuario.Id });
                jobId = await cmd.ExecuteScalarAsync(ct) is long id
                    ? id
                    : throw ErroNegocioException.Conflito("Já existe um teste em andamento para esta conta.");
            }

            await AuditarAsync(conn, tx, contaId, existente.Id, "TESTE_SOLICITADO", $"job {jobId}", usuario.Id, ct);
            return jobId;
        }, ct);

    public Task<List<AuditoriaItem>> AuditoriaAsync(UsuarioAtual usuario, long contaId, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            await ContaAsync(conn, tx, contaId, ct);
            var lista = new List<AuditoriaItem>();
            await using var cmd = new NpgsqlCommand(CofreSql.Auditoria, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                lista.Add(new AuditoriaItem(
                    r.GetString(0),
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.IsDBNull(2) ? "Sistema" : r.GetString(2),
                    r.GetString(3),
                    r.GetFieldValue<DateTimeOffset>(4)));
            }
            return lista;
        }, ct);

    // ── Auxiliares ───────────────────────────────────────────────────────────

    /// <summary>Conta visível ao usuário (RLS). Invisível ou inexistente → 404.</summary>
    private static async Task<(long ClienteId, string Provedor, string Identificador)> ContaAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, long contaId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(CofreSql.Conta, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw ErroNegocioException.NaoEncontrado("Conta não encontrada.");
        return (r.GetInt64(0), r.GetString(1), r.GetString(2));
    }

    private static async Task<CredencialMetadados> LerMetadadosAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long contaId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(CofreSql.Metadados, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            return CredencialMetadados.Inexistente;
        return new CredencialMetadados(
            true,
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.GetString(3),
            r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6),
            r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7));
    }

    private static async Task<(long Id, string Status)?> CredencialIdAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long contaId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(CofreSql.CredencialId, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetInt64(0), r.GetString(1)) : null;
    }

    /// <summary>
    /// DEK ativa do cliente, decifrada com a KEK da versão em que foi cifrada; se não existir, cria
    /// (32 bytes aleatórios, cifrada com a KEK atual) na mesma transação. O chamador zera a DEK.
    /// </summary>
    private async Task<(long Id, byte[] Dek)> ObterOuCriarDekAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, CancellationToken ct)
    {
        await using (var trava = new NpgsqlCommand(CofreSql.TravarDekCliente, conn, tx))
        {
            trava.Parameters.Add(new NpgsqlParameter("namespace", NpgsqlDbType.Integer) { Value = NamespaceTravaDek });
            trava.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Integer) { Value = unchecked((int)clienteId) });
            await trava.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(CofreSql.DekAtiva, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                var id = r.GetInt64(0);
                var versaoKek = (int)r.GetInt16(1);
                var dekCifrada = r.GetFieldValue<byte[]>(2);
                var dek = CofreCripto.Decifrar(_chaves.Kek(versaoKek), dekCifrada, CofreCripto.AadDek(clienteId, versaoKek));
                return (id, dek);
            }
        }

        var (versaoAtual, kek) = _chaves.KekAtual();
        var nova = RandomNumberGenerator.GetBytes(CofreCripto.TamanhoChave);
        var cifrada = CofreCripto.Cifrar(kek, nova, CofreCripto.AadDek(clienteId, versaoAtual));

        await using var ins = new NpgsqlCommand(CofreSql.InserirDek, conn, tx);
        ins.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        ins.Parameters.Add(new NpgsqlParameter("versao_kek", NpgsqlDbType.Smallint) { Value = (short)versaoAtual });
        ins.Parameters.Add(new NpgsqlParameter("dek_cifrada", NpgsqlDbType.Bytea) { Value = cifrada });
        var novoId = (long)(await ins.ExecuteScalarAsync(ct))!;
        return (novoId, nova);
    }

    private static async Task AuditarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long contaId, long? credencialId,
        string acao, string? detalhe, long usuarioId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(CofreSql.InserirAuditoria, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
        cmd.Parameters.Add(new NpgsqlParameter("credencial_id", NpgsqlDbType.Bigint) { Value = (object?)credencialId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("acao", NpgsqlDbType.Varchar) { Value = acao });
        cmd.Parameters.Add(new NpgsqlParameter("detalhe", NpgsqlDbType.Varchar) { Value = (object?)detalhe ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = usuarioId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
