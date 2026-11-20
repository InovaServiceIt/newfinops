using CloudVista.Worker;
using Finops.Backend.Cofre;

namespace Finops.Backend.Tests;

public class WorkerTests
{
    // ── Cron e fuso ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0 0 6 * * ?", "America/Sao_Paulo")]       // todo dia às 06:00
    [InlineData("0 30 */2 ? * MON-FRI", "America/Sao_Paulo")]
    [InlineData("0 0 3 1 * ? 2027", "UTC")]                // 7 campos (com ano)
    [InlineData("0 0 6 * * ?", null)]                      // fuso nulo → America/Sao_Paulo
    [InlineData("0 0 6 * * ?", "  ")]
    public void Cron_e_fuso_validos(string cron, string? fuso)
    {
        var r = ConversaoAgenda.Converter(cron, fuso);
        Assert.True(r.Valida, r.Motivo);
        Assert.NotNull(r.Fuso);
    }

    [Fact]
    public void Fuso_padrao_e_sao_paulo() =>
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo").BaseUtcOffset,
            ConversaoAgenda.Converter("0 0 6 * * ?", null).Fuso!.BaseUtcOffset);

    [Theory]
    [InlineData("0 6 * * *", "6 ou 7 campos")]          // cron de 5 campos (Unix) não serve
    [InlineData("0 0 6 * * ? 2027 extra", "6 ou 7 campos")]
    [InlineData("0 0 25 * * ?", "cron inválido")]         // hora 25
    [InlineData("0 0 6 * * *", "cron inválido")]          // Quartz exige ? em dia do mês ou da semana
    [InlineData("", "cron vazio")]
    public void Cron_invalido(string cron, string motivo)
    {
        var r = ConversaoAgenda.Converter(cron, "America/Sao_Paulo");
        Assert.False(r.Valida);
        Assert.Contains(motivo, r.Motivo);
    }

    [Fact]
    public void Fuso_inexistente()
    {
        var r = ConversaoAgenda.Converter("0 0 6 * * ?", "Marte/Olympus_Mons");
        Assert.False(r.Valida);
        Assert.Equal("fuso inexistente", r.Motivo);
    }

    [Fact]
    public void Assinatura_muda_quando_cron_ou_fuso_muda()
    {
        var a = new AgendaItem(1, "x", "COLETA_CUSTO", null, null, "0 0 6 * * ?", null, "{}");
        Assert.Equal(ConversaoAgenda.Assinatura(a), ConversaoAgenda.Assinatura(a with { Fuso = "America/Sao_Paulo" }));
        Assert.NotEqual(ConversaoAgenda.Assinatura(a), ConversaoAgenda.Assinatura(a with { Cron = "0 0 7 * * ?" }));
        Assert.NotEqual(ConversaoAgenda.Assinatura(a), ConversaoAgenda.Assinatura(a with { Fuso = "UTC" }));
    }

    // ── Zelador ─────────────────────────────────────────────────────────────

    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(60);

    [Fact]
    public void Zelador_mantem_job_com_sinal_recente()
    {
        var job = new JobEmExecucao(1, 1, Agora.AddMinutes(-59), Agora.AddHours(-5), Agora.AddHours(-6));
        Assert.Equal(DecisaoZelador.Manter, RegraZelador.Decidir(job, Agora, Timeout, 3));
    }

    [Fact]
    public void Zelador_no_limite_exato_ainda_mantem()
    {
        var job = new JobEmExecucao(1, 1, Agora.AddMinutes(-60), null, Agora.AddHours(-2));
        Assert.Equal(DecisaoZelador.Manter, RegraZelador.Decidir(job, Agora, Timeout, 3));
    }

    [Fact]
    public void Zelador_devolve_para_pendente_se_ainda_ha_tentativas()
    {
        var job = new JobEmExecucao(1, 2, Agora.AddMinutes(-61), null, Agora.AddHours(-2));
        Assert.Equal(DecisaoZelador.VoltarParaPendente, RegraZelador.Decidir(job, Agora, Timeout, 3));
    }

    [Fact]
    public void Zelador_marca_erro_quando_atingiu_o_maximo()
    {
        var job = new JobEmExecucao(1, 3, Agora.AddMinutes(-61), null, Agora.AddHours(-2));
        Assert.Equal(DecisaoZelador.MarcarErro, RegraZelador.Decidir(job, Agora, Timeout, 3));
        Assert.Equal("Tempo de execução excedido", RegraZelador.MensagemTimeout);
    }

    [Fact]
    public void Zelador_sem_heartbeat_usa_iniciado_em_e_depois_criado_em()
    {
        var semHeartbeat = new JobEmExecucao(1, 0, null, Agora.AddMinutes(-10), Agora.AddHours(-9));
        Assert.Equal(DecisaoZelador.Manter, RegraZelador.Decidir(semHeartbeat, Agora, Timeout, 3));

        var soCriacao = new JobEmExecucao(1, 0, null, null, Agora.AddMinutes(-90));
        Assert.Equal(DecisaoZelador.VoltarParaPendente, RegraZelador.Decidir(soCriacao, Agora, Timeout, 3));
    }

    // ── Parâmetros com default ──────────────────────────────────────────────

    [Theory]
    [InlineData("45", 45)]
    [InlineData(" 30 ", 30)]
    [InlineData(null, 60)]
    [InlineData("", 60)]
    [InlineData("abc", 60)]
    [InlineData("0", 60)]        // abaixo do mínimo
    [InlineData("99999", 60)]    // acima do máximo
    [InlineData("1.5", 60)]
    public void Parametro_inteiro_com_default(string? valor, int esperado) =>
        Assert.Equal(esperado, Parametros.Inteiro(valor, Parametros.TimeoutExecucaoMinPadrao, 1, 1440));

    [Fact]
    public void Defaults_seguros() =>
        Assert.Equal((60, 3, 60), (Parametros.TimeoutExecucaoMinPadrao, Parametros.MaxTentativasPadrao, Parametros.SincronizarAgendaSegPadrao));

    [Theory]
    [InlineData(null, "{}")]
    [InlineData("", "{}")]
    [InlineData("[1,2]", "{}")]
    [InlineData("não é json", "{}")]
    [InlineData("{\"servicos\":[\"compute\"]}", "{\"servicos\":[\"compute\"]}")]
    public void Escopo_da_agenda_sempre_objeto_json(string? json, string esperado) =>
        Assert.Equal(esperado, Json.ObjetoOuVazio(json));

    // ── SQL: contas alvo e "já existe" ──────────────────────────────────────

    [Fact]
    public void Contas_alvo_ativas_de_cliente_ativo_com_credencial_ativa_e_filtros()
    {
        var sql = WorkerSql.EnfileirarDaAgenda;
        Assert.Contains("cr.status = 'ATIVA'", sql);
        Assert.Contains("WHERE cc.ativo", sql);
        Assert.Contains("AND c.ativo", sql);
        Assert.Contains("(@cliente_id IS NULL OR cc.cliente_id = @cliente_id)", sql);
        Assert.Contains("(@cloud_conta_id IS NULL OR cc.id = @cloud_conta_id)", sql);
        Assert.Contains("'AGENDADA'", sql);
        Assert.Contains("@prioridade", sql);
        Assert.Contains("@agenda_id", sql);
        // A contagem usa exatamente o mesmo filtro (para logar quantas foram puladas).
        Assert.EndsWith(WorkerSql.ContarContasAlvo["SELECT count(*) ".Length..], sql[..sql.IndexOf("\nON CONFLICT", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Job_em_andamento_duplicado_vira_ja_existe_sem_erro()
    {
        // Índice único parcial (conta, tipo) em PENDENTE/EXECUTANDO: ON CONFLICT DO NOTHING em vez de 23505.
        Assert.Contains("ON CONFLICT DO NOTHING", WorkerSql.EnfileirarDaAgenda);
        Assert.Contains("ON CONFLICT DO NOTHING", CofreSql.EnfileirarTeste);
        Assert.Contains("'TESTE_CREDENCIAL'", CofreSql.EnfileirarTeste);
        Assert.Contains("'MANUAL'", CofreSql.EnfileirarTeste);
        Assert.Contains(", 10)", CofreSql.EnfileirarTeste);
    }

    [Fact]
    public void Diagnostico_loga_mensagem_so_de_excecao_npgsql_e_corta()
    {
        var conexao = new Npgsql.NpgsqlException("Failed to connect to 10.0.0.5:5432", new System.Net.Sockets.SocketException(10060));
        Assert.Contains("Failed to connect to 10.0.0.5:5432", Diagnostico.De(conexao));
        Assert.Contains("SocketException", Diagnostico.De(conexao));

        // A de parse da connection string (ecoa a senha) nunca entra: só o tipo é logado.
        Assert.Equal("-", Diagnostico.De(new ArgumentException("Couldn't set SEGREDO_TESTE;password")));
        Assert.Equal("-", Diagnostico.De(new InvalidOperationException("x")));

        Assert.True(Diagnostico.De(new Npgsql.NpgsqlException(new string('a', 1000))).Length <= Diagnostico.Limite);
    }

    [Fact]
    public void Zelador_so_mexe_em_jobs_ainda_executando()
    {
        Assert.Contains("FOR UPDATE SKIP LOCKED", WorkerSql.ExecutandoParaZelador);
        Assert.Contains("AND status = 'EXECUTANDO'", WorkerSql.VoltarParaPendente);
        Assert.Contains("AND status = 'EXECUTANDO'", WorkerSql.MarcarErroTimeout);
        Assert.DoesNotContain("tentativas", WorkerSql.VoltarParaPendente); // mantém tentativas
    }
}
