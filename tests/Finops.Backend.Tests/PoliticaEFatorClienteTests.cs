using System.Reflection;
using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Custos;
using Finops.Backend.Governanca;
using Finops.Backend.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Compartilhado = Finops.Backend.Compartilhado;

namespace Finops.Backend.Tests;

/// <summary>MD 1.5: CLIENTE sempre com fator (Parte A) e política de tags (Parte B).</summary>
public class PoliticaEFatorClienteTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 20);
    private static DateOnly D(int a, int m, int d) => new(a, m, d);
    private static LinhaAgregada L(DateOnly p, string chave, decimal v) => new(p, chave, "", "BRL", v);
    private static FatorCliente Fator(decimal f, DateOnly desde) => new(1, 1, "C", null, null, f, desde, null, DateTimeOffset.UnixEpoch, "x");

    private static DadosVisaoGeral Dados(IReadOnlyList<FatorCliente> fatores) => new(
        [L(D(2026, 10, 1), "Compute", 300m), L(D(2026, 11, 1), "Compute", 50m)],
        Enumerable.Range(1, 19).Select(d => L(D(2026, 11, d), "Compute", 10m)).Append(L(D(2026, 10, 31), "Compute", 7m)).ToList(),
        [L(D(2026, 11, 1), "raiz/prod", 40m)], [L(D(2026, 11, 1), "ocid1.instance.oc1..a", 20m)],
        [L(D(2026, 11, 25), "", 12m)], [L(D(2026, 11, 1), "", -5m)],
        D(2026, 11, 19), 3, DateTimeOffset.Parse("2026-11-20T10:00:00Z"), fatores);

    // ── Parte A ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cliente_recebe_o_mesmo_valor_que_o_admin_com_fator()
    {
        var fatores = new[] { Fator(1.3m, D(2025, 1, 1)) };
        var admin = VisaoGeralCustos.Montar(1, null, Hoje, Dados(fatores), comFatorPedido: true);
        var cliente = VisaoGeralCustos.Montar(1, null, Hoje, Dados(fatores), comFatorPedido: false, cliente: true);
        Assert.Equal(admin.Moedas.Single().Kpis, cliente.Moedas.Single().Kpis);
        Assert.Equal(admin.Moedas.Single().Mensal.Select(p => p.Total), cliente.Moedas.Single().Mensal.Select(p => p.Total));
        Assert.Equal(admin.Moedas.Single().TopRecursos.Select(i => i.Valor), cliente.Moedas.Single().TopRecursos.Select(i => i.Valor));
        Assert.True(cliente.ComFator);
        Assert.Equal(247m, cliente.Moedas.Single().Kpis.MesCorrente);   // 190 × 1,3

        var c = ExploradorRegras.Validar(1, null, "DIA", D(2026, 11, 1), D(2026, 11, 19), "SERVICO", null, null, null, true, 10);
        var linhas = Dados(fatores).ServicoDia;
        var exAdmin = ExploradorRegras.Montar(c, FatorRegras.Aplicar(linhas, fatores, null), null, new Dictionary<string, RotuloChave>(), true, true, []);
        var exCliente = ExploradorRegras.Montar(c, FatorRegras.AplicarEstrito(linhas, fatores, null), null, new Dictionary<string, RotuloChave>(),
            true, true, [], p => FatorRegras.EmApuracao(fatores, null, p));
        Assert.Equal(exAdmin.Total, exCliente.Total);
        Assert.Equal(exAdmin.Serie.Select(p => p.Grupos.Sum(g => g.Valor)), exCliente.Serie.Select(p => p.Grupos.Sum(g => g.Valor)));
    }

    [Fact]
    public void Cliente_sem_fator_vigente_nao_ve_numero()
    {
        var r = VisaoGeralCustos.Montar(1, null, Hoje, Dados([]), comFatorPedido: false, cliente: true);
        Assert.Empty(r.Moedas);
        Assert.True(r.EmApuracao);
        Assert.Null(r.FatorAtual);
        Assert.Null(r.FatorDisponivel);

        var c = ExploradorRegras.Validar(1, null, "MES", D(2026, 9, 1), D(2026, 11, 1), "SERVICO", null, null, null, false, 10);
        var ex = ExploradorRegras.Montar(c, FatorRegras.AplicarEstrito(Dados([]).ServicoMes, [], null), -5m,
            new Dictionary<string, RotuloChave>(), true, false, [], p => FatorRegras.EmApuracao([], null, p));
        Assert.Null(ex.Total);
        Assert.Null(ex.TotalPeriodoAnterior);
        Assert.Null(ex.Creditos);
        Assert.True(ex.EmApuracao);
        Assert.All(ex.Serie, p => Assert.True(p.EmApuracao));
        Assert.Empty(ex.Tabela);
        Assert.Single(ExploradorCsv.Linhas(ex, cliente: true));   // só o cabeçalho
    }

    [Fact]
    public void Cliente_com_fator_a_partir_do_meio_do_mes_ve_so_o_apurado()
    {
        var r = VisaoGeralCustos.Montar(1, null, Hoje, Dados([Fator(1.5m, D(2026, 11, 10))]), false, cliente: true);
        var m = r.Moedas.Single();
        Assert.Null(m.Kpis.MesCorrente);       // dias 1–9 sem fator
        Assert.Null(m.Kpis.PrevisaoMes);
        Assert.Null(m.Kpis.MesAnterior);
        Assert.Null(m.Kpis.Ytd);
        Assert.Null(m.Kpis.CreditosMes);       // 1º/11 sem fator
        Assert.True(m.MesEmApuracao);
        Assert.Empty(m.Donut);
        Assert.Empty(m.TopRecursos);
        Assert.True(m.Mensal.Single(p => p.Mes == "2026-11").EmApuracao);
        Assert.Equal(0m, m.Mensal.Single(p => p.Mes == "2026-11").Total);
        Assert.True(m.Diario.Single(p => p.Dia == "2026-11-09").EmApuracao);
        var dia10 = m.Diario.Single(p => p.Dia == "2026-11-10");
        Assert.False(dia10.EmApuracao);
        Assert.Equal(15m, dia10.Valores["Compute"]);   // 10 × 1,5
    }

    private static IEnumerable<string> Propriedades(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
            {
                yield return p.Name;
                foreach (var n in Propriedades(p.Value)) yield return n;
            }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var i in e.EnumerateArray())
                foreach (var n in Propriedades(i)) yield return n;
    }

    [Fact]
    public void Json_do_cliente_nao_tem_fator_nem_custo_bruto()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var proibidas = new[] { "fator", "semFator", "custoNuvem", "fatorAtual", "fatorDisponivel" };
        var fatores = new[] { Fator(1.3m, D(2025, 1, 1)) };

        var vg = VisaoGeralCustos.Montar(1, null, Hoje, Dados(fatores), false, cliente: true);
        var c = ExploradorRegras.Validar(1, null, "MES", D(2026, 9, 1), D(2026, 11, 1), "SERVICO", null, null, null, false, 10);
        var ex = ExploradorRegras.Montar(c, FatorRegras.AplicarEstrito(Dados(fatores).ServicoMes, fatores, null), -5m,
            new Dictionary<string, RotuloChave>(), true, true, [], p => FatorRegras.EmApuracao(fatores, null, p));

        foreach (var obj in new object[] { vg, ex })
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(obj, obj.GetType(), web));
            var nomes = Propriedades(doc.RootElement).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var p in proibidas) Assert.DoesNotContain(p, nomes);
        }
        // CSV do CLIENTE: sem coluna de fator.
        Assert.DoesNotContain("fator", ExploradorCsv.Linhas(ex, cliente: true)[0]);

        // Admin continua recebendo o fator.
        var admin = VisaoGeralCustos.Montar(1, null, Hoje, Dados(fatores), true);
        using var docAdmin = JsonDocument.Parse(JsonSerializer.Serialize(admin, web));
        Assert.Equal(1.3m, docAdmin.RootElement.GetProperty("fatorAtual").GetDecimal());
    }

    private static string? Politica(MemberInfo m) => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault();

    [Fact]
    public void Endpoint_de_fator_continua_so_admin_e_analista()
    {
        Assert.Equal(Politicas.Analista, Politica(typeof(FatorClienteController)));
        Assert.Equal(Politicas.Administrador, Politica(typeof(FatorClienteController).GetMethod(nameof(FatorClienteController.Criar))!));
        Assert.Equal(Politicas.Administrador, Politica(typeof(FatorClienteController).GetMethod(nameof(FatorClienteController.Atualizar))!));
    }

    // ── Parte B ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cliente_nao_escreve_na_politica_403_antes_do_banco()
    {
        Assert.Equal(Politicas.Autorizado, Politica(typeof(PoliticaTagsController)));
        Assert.Equal(Politicas.Analista, Politica(typeof(PoliticaTagsController).GetMethod(nameof(PoliticaTagsController.Criar))!));
        Assert.Equal(Politicas.Analista, Politica(typeof(PoliticaTagsController).GetMethod(nameof(PoliticaTagsController.Atualizar))!));
        Assert.Null(Politica(typeof(PoliticaTagsController).GetMethod(nameof(PoliticaTagsController.Listar))!));
        Assert.Null(Politica(typeof(PoliticaTagsController).GetMethod(nameof(PoliticaTagsController.Evolucao))!));
    }

    [Fact]
    public void Validacao_da_politica()
    {
        var v = PoliticaRegras.Validar(new PoliticaEntrada(1, " MetroRio_Tags.Resource ", ["Produção", "producao", " ", "Homologação"], " x ", null), criacao: true);
        Assert.Equal("MetroRio_Tags.Resource", v.Chave);
        Assert.Equal(new[] { "Produção", "Homologação" }, v.Valores);   // duplicata normalizada e vazio removidos
        Assert.Equal("x", v.Descricao);
        Assert.Null(PoliticaRegras.Validar(new PoliticaEntrada(1, "a", ["  "], null, null), true).Valores);   // vazia = qualquer valor
        Assert.Null(PoliticaRegras.Validar(new PoliticaEntrada(1, "a", null, null, null), true).Valores);
        Assert.Throws<ErroNegocioException>(() => PoliticaRegras.Validar(new PoliticaEntrada(null, "a", null, null, null), true));
        Assert.Throws<ErroNegocioException>(() => PoliticaRegras.Validar(new PoliticaEntrada(1, "Área", null, null, null), true));
        Assert.Throws<ErroNegocioException>(() => PoliticaRegras.Validar(new PoliticaEntrada(1, "a", Enumerable.Range(0, 101).Select(i => $"v{i}").ToList(), null, null), true));
        Assert.Throws<ErroNegocioException>(() => PoliticaRegras.Validar(new PoliticaEntrada(1, "a", null, new string('d', 501), null), true));
        Assert.Throws<ErroNegocioException>(() => PoliticaRegras.Periodo(D(2025, 1, 1), D(2026, 1, 2), Hoje));
    }

    [Fact]
    public void Normalizacao_sql_igual_a_do_csharp()
    {
        var de = Compartilhado.TagsSql.AcentosDe;
        var para = Compartilhado.TagsSql.AcentosPara;
        Assert.Equal(de.Length, para.Length);
        for (var i = 0; i < de.Length; i++)
            Assert.Equal(para[i].ToString(), GovernancaRegras.Normalizar(de[i].ToString()));
        var norm = Compartilhado.TagsSql.Norm("v");
        Assert.Contains("lower(translate(btrim(v)", norm);
        Assert.Contains(@"'\s+', ' ', 'g'", norm);
    }

    [Fact]
    public void Sql_de_conformidade_com_e_sem_valores_permitidos_e_sem_automaticas()
    {
        var sql = Compartilhado.PoliticaSql.Resumo;
        Assert.Contains("p.valores_permitidos IS NOT NULL AS restrita", sql);
        Assert.Contains("(NOT pol.restrita OR " + Compartilhado.TagsSql.Norm("pr.valor") + " = ANY(pol.permitidos))", sql);
        Assert.Contains("btrim(pr.valor) <> ''", sql);
        Assert.Contains("HAVING bool_and(ok)", sql);
        Assert.Contains("WHERE NOT (" + Compartilhado.TagsSql.Automatica + ")", sql);
        Assert.Contains("p.cliente_id = @cliente_id AND p.ativa", sql);
        Assert.Contains("r.removido_em IS NULL", sql);
        Assert.DoesNotContain("SELECT *", sql);
        Assert.Contains("WHERE EXISTS (SELECT 1 FROM pol)", Compartilhado.PoliticaSql.UpsertSnapshot);   // sem política: sem snapshot
        Assert.Contains("ON CONFLICT (cliente_id, dia) DO UPDATE", Compartilhado.PoliticaSql.UpsertSnapshot);
        Assert.Contains("timezone('America/Sao_Paulo', @visto)", Compartilhado.PoliticaSql.UpsertSnapshot);
    }

    [Fact]
    public void Governanca_no_modo_politica_usa_valores_permitidos()
    {
        var sql = GovernancaSql.Politica(GovernancaSql.AvaliacaoGeral);
        Assert.DoesNotContain(GovernancaSql.PresentesSimples, sql);
        Assert.Contains("JOIN pol ON pol.tag = pr.tag", sql);
        Assert.Contains("= ANY(pol.permitidos)", sql);
        Assert.Contains(GovernancaSql.PresentesSimples, GovernancaSql.AvaliacaoGeral);   // ad hoc intacta
    }

    [Fact]
    public void Recalcula_quando_a_politica_muda()
    {
        var sql = Compartilhado.PoliticaSql.ClientesParaRecalcular;
        Assert.Contains("max(COALESCE(p.atualizado_em, p.criado_em)) > COALESCE(s.calculado_em, '-infinity'::timestamptz)", sql);
        Assert.Contains("s.calculado_em IS NULL AND EXISTS (SELECT 1 FROM recurso r", sql);
        Assert.Contains("HAVING bool_or(p.ativa)", sql);
        // O worker compila o MESMO arquivo (namespace próprio): mesma SQL.
        Assert.Equal(sql, CloudVista.Worker.PoliticaSql.ClientesParaRecalcular);
        Assert.Equal(Compartilhado.PoliticaSql.UpsertSnapshot, CloudVista.Worker.PoliticaSql.UpsertSnapshot);
    }

    [Fact]
    public async Task Snapshot_que_falha_nao_derruba_a_importacao()
    {
        string? registrado = null;
        var aviso = await CloudVista.Worker.ImportadorRegras.TentarSnapshotAsync(
            () => throw new InvalidOperationException("banco fora"), a => { registrado = a; return Task.CompletedTask; });
        Assert.NotNull(aviso);
        Assert.Equal(aviso, registrado);
        Assert.DoesNotContain("banco fora", aviso);   // só tipo, sem texto de exceção

        // Mesmo se registrar o aviso falhar, não lança.
        var aviso2 = await CloudVista.Worker.ImportadorRegras.TentarSnapshotAsync(
            () => throw new InvalidOperationException(), _ => throw new TimeoutException());
        Assert.NotNull(aviso2);

        Assert.Null(await CloudVista.Worker.ImportadorRegras.TentarSnapshotAsync(() => Task.CompletedTask, _ => Task.CompletedTask));
        Assert.Contains("to_jsonb(CAST(@aviso AS text))", CloudVista.Worker.ImportadorSql.AcrescentarAviso);
    }
}
