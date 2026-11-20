using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudVista.Worker;

namespace Finops.Backend.Tests;

/// <summary>
/// Fixture sintética com a MESMA estrutura do bloco "cost" gerado por
/// coletor/vendor/baseline_oci/collector/collectors/cost.py (_agg_rows, forecast, credits, queries).
/// </summary>
public class ImportadorTests
{
    private const string Fixture = """
    {
      "source": "usage_api.request_summarized_usages",
      "currencies": ["BRL"],
      "monthly_by_service": [
        {"month": "2026-09", "service": "Compute", "amount": 1000.10, "currency": "BRL"},
        {"month": "2026-10", "service": "Compute", "amount": 1100.00, "currency": "BRL"},
        {"month": "2026-10", "service": "Compute", "amount": 50.00, "currency": "BRL"},
        {"month": "2026-11", "service": "Object Storage", "amount": 20.50, "currency": "BRL"}
      ],
      "daily_by_service": [
        {"day": "2026-11-18", "service": "Compute", "amount": 35.00, "currency": "BRL"},
        {"day": "2026-11-19", "service": "Compute", "amount": 36.25, "currency": "BRL"}
      ],
      "monthly_by_compartment": [
        {"month": "2026-11", "compartment_path": "raiz/prod", "amount": 700.00, "currency": "BRL"}
      ],
      "daily_by_compartment": [
        {"day": "2026-11-19", "compartment_path": "raiz/prod", "amount": 30.00, "currency": "BRL"}
      ],
      "monthly_by_resource": [
        {"month": "2026-11", "resource_id": "ocid1.instance.oc1..aaa", "service": "Compute", "amount": 400.00, "currency": "BRL", "resource_name": "vm-01"},
        {"month": "2026-11", "resource_id": "ocid1.bucket.oc1..bbb", "amount": 5.00, "currency": null}
      ],
      "monthly_by_tag": [
        {"month": "2026-11", "tag_value": "financeiro", "amount": 300.00, "currency": "BRL", "tag_namespace": "CostCenter", "tag_key": "area"},
        {"month": "2026-11", "tag_value": "ti", "amount": 200.00, "currency": "BRL", "tag_namespace": "Projeto", "tag_key": "nome"}
      ],
      "forecast": {
        "forecast_type": "BASIC",
        "daily": [
          {"day": "2026-11-19", "amount": 36.25, "currency": "BRL", "is_forecast": false},
          {"day": "2026-11-20", "amount": 37.00, "currency": "BRL", "is_forecast": true},
          {"day": "2026-11-21", "amount": 37.50, "currency": "BRL", "is_forecast": true}
        ]
      },
      "credits": { "query_type": "ALLCREDIT", "monthly": [ {"month": "2026-11", "amount": -12.34, "currency": "BRL"} ] },
      "queries": [
        {"name": "monthly_by_service", "rows_kept": 4, "truncated": false, "ok": true},
        {"name": "monthly_by_compartment", "rows_kept": 1, "truncated": false, "ok": true},
        {"name": "daily_by_service", "rows_kept": 2, "truncated": false, "ok": true},
        {"name": "daily_by_compartment", "rows_kept": 1, "truncated": true, "ok": true},
        {"name": "forecast_daily_total", "rows_kept": 3, "truncated": false, "ok": true},
        {"name": "credits_monthly", "rows_kept": 1, "truncated": false, "ok": true},
        {"name": "monthly_by_tag.CostCenter.area", "truncated": false, "ok": true},
        {"name": "monthly_by_tag.Projeto.nome", "truncated": false, "ok": false, "error": "timeout"},
        {"name": "monthly_by_resource", "truncated": true, "ok": false},
        {"name": "monthly_by_resource", "truncated": false, "ok": true}
      ]
    }
    """;

    private static PlanoImportacao Plano(string json = Fixture)
    {
        using var doc = JsonDocument.Parse(json);
        return ParserCustoOci.Planejar(doc.RootElement);
    }

    private static BlocoFato Bloco(PlanoImportacao p, string nome) => p.Fatos.Single(b => b.Nome == nome);

    [Fact]
    public void Seis_blocos_mais_tags_previsao_e_credito()
    {
        var p = Plano();
        Assert.Equal(new[] { "monthly_by_service", "daily_by_service", "monthly_by_compartment", "daily_by_compartment",
            "monthly_by_resource", "monthly_by_tag.CostCenter.area", "monthly_by_tag.Projeto.nome" }, p.Fatos.Select(b => b.Nome));
        Assert.Equal(("SERVICO", "MES"), (Bloco(p, "monthly_by_service").Dimensao, Bloco(p, "monthly_by_service").Granularidade));
        Assert.Equal(("COMPARTIMENTO", "DIA"), (Bloco(p, "daily_by_compartment").Dimensao, Bloco(p, "daily_by_compartment").Granularidade));
    }

    [Fact]
    public void Chaves_repetidas_no_mesmo_periodo_sao_somadas()
    {
        var outubro = Bloco(Plano(), "monthly_by_service").Linhas.Single(l => l.Periodo == new DateOnly(2026, 10, 1));
        Assert.Equal(1150.00m, outubro.Valor);
    }

    [Fact]
    public void Intervalo_de_delete_e_o_menor_e_o_maior_periodo_recebidos()
    {
        var p = Plano();
        var mensal = Bloco(p, "monthly_by_service");
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 1)), (mensal.De!.Value, mensal.Ate!.Value));
        var diario = Bloco(p, "daily_by_service");
        Assert.Equal((new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 19)), (diario.De!.Value, diario.Ate!.Value));
    }

    [Fact]
    public void Bloco_truncado_ou_com_falha_nao_apaga_e_gera_aviso()
    {
        var p = Plano();
        var truncado = Bloco(p, "daily_by_compartment");
        Assert.False(truncado.Aplicar);
        Assert.Empty(truncado.Linhas);
        Assert.Contains(p.Avisos, a => a.Contains("daily_by_compartment") && a.Contains("truncada"));

        var tagFalhou = Bloco(p, "monthly_by_tag.Projeto.nome");
        Assert.False(tagFalhou.Aplicar);
        Assert.Contains(p.Avisos, a => a.Contains("Projeto.nome") && a.Contains("falhou"));
    }

    [Fact]
    public void Recurso_usa_a_ultima_ocorrencia_da_consulta_e_chave2_e_o_servico()
    {
        var recurso = Bloco(Plano(), "monthly_by_resource");
        Assert.True(recurso.Aplicar);
        var vm = recurso.Linhas.Single(l => l.Chave == "ocid1.instance.oc1..aaa");
        Assert.Equal(("Compute", 400.00m), (vm.Chave2, vm.Valor));
        Assert.Equal("", recurso.Linhas.Single(l => l.Chave == "ocid1.bucket.oc1..bbb").Chave2);
    }

    [Fact]
    public void Moeda_ausente_vira_XXX_com_aviso()
    {
        var p = Plano();
        Assert.Equal("XXX", Bloco(p, "monthly_by_resource").Linhas.Single(l => l.Chave == "ocid1.bucket.oc1..bbb").Moeda);
        Assert.Contains(p.Avisos, a => a.Contains("XXX"));
    }

    [Fact]
    public void Tag_chave_e_valor_chave2_namespace_key_e_delete_restrito_a_tag()
    {
        var tag = Bloco(Plano(), "monthly_by_tag.CostCenter.area");
        Assert.Equal(("TAG", "MES", "CostCenter.area"), (tag.Dimensao, tag.Granularidade, tag.FiltroChave2));
        var l = Assert.Single(tag.Linhas);
        Assert.Equal(("financeiro", "CostCenter.area", 300.00m), (l.Chave, l.Chave2, l.Valor));
    }

    [Fact]
    public void Previsao_so_dias_projetados_e_credito_mensal()
    {
        var p = Plano();
        Assert.Equal(new[] { new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 21) }, p.Previsao.Linhas.Select(l => l.Periodo));
        Assert.Equal(74.50m, p.Previsao.Linhas.Sum(l => l.Valor));
        var credito = Assert.Single(p.Credito.Linhas);
        Assert.Equal((new DateOnly(2026, 11, 1), -12.34m), (credito.Periodo, credito.Valor));
    }

    [Fact]
    public void Resumo_da_importacao()
    {
        var r = Plano().Resumo();
        var linhas = (Dictionary<string, int>)r["linhas"]!;
        Assert.Equal(3, linhas["servico_mes"]);
        Assert.Equal(2, linhas["servico_dia"]);
        Assert.Equal(0, linhas["compartimento_dia"]);   // truncado
        Assert.Equal(1, linhas["tag_mes"]);              // só a tag que deu certo
        Assert.Equal(2, linhas["previsao"]);
        Assert.Equal(new Dictionary<string, string> { ["de"] = "2026-11-18", ["ate"] = "2026-11-19" }, r["periodo_custo"]);
        Assert.Equal(new[] { "BRL", "XXX" }, (IEnumerable<string>)r["moedas"]!);
    }

    [Fact]
    public void Bloco_ausente_nao_apaga()
    {
        var p = Plano("""{ "monthly_by_service": [], "queries": [] }""");
        Assert.False(Bloco(p, "daily_by_service").Aplicar);
        Assert.True(Bloco(p, "monthly_by_service").Aplicar);
        Assert.Null(Bloco(p, "monthly_by_service").De);   // vazio: nenhum intervalo, nada apagado
        Assert.False(p.Previsao.Aplicar);
    }

    // ── coleta_bruta ────────────────────────────────────────────────────────

    private static byte[] Gzip(string texto)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(Encoding.UTF8.GetBytes(texto));
        return ms.ToArray();
    }

    [Fact]
    public void Sha256_confere_e_divergente()
    {
        var gz = Gzip("{\"cost\":{}}");
        var sha = Convert.ToHexString(SHA256.HashData(gz)).ToLowerInvariant();
        Assert.True(ColetaBruta.Sha256Confere(gz, sha));
        Assert.True(ColetaBruta.Sha256Confere(gz, sha.ToUpperInvariant()));
        Assert.False(ColetaBruta.Sha256Confere(gz, new string('0', 64)));
        Assert.False(ColetaBruta.Sha256Confere(gz, null));
        gz[^1] ^= 1;
        Assert.False(ColetaBruta.Sha256Confere(gz, sha));
    }

    [Fact]
    public void Descomprime_o_gzip_do_coletor()
    {
        Assert.Equal("{\"cost\":{}}", Encoding.UTF8.GetString(ColetaBruta.Descomprimir(Gzip("{\"cost\":{}}"))));
        Assert.Throws<InvalidDataException>(() => ColetaBruta.Descomprimir(Encoding.UTF8.GetBytes("não é gzip")));
    }

    [Fact]
    public void Sql_idempotente()
    {
        Assert.Contains("periodo BETWEEN @de AND @ate", ImportadorSql.ApagarFato);
        Assert.Contains("(@chave2 IS NULL OR chave2 = @chave2)", ImportadorSql.ApagarFato);
        Assert.Contains("ON CONFLICT (cloud_conta_id, granularidade, periodo, dimensao, chave, chave2, moeda)", ImportadorSql.InserirFato);
        Assert.Contains("FOR UPDATE SKIP LOCKED", ImportadorSql.Claim);
        Assert.Contains("status = 'COLETADO'", ImportadorSql.Claim);
        Assert.Contains("AND status = 'IMPORTANDO'", ImportadorSql.Concluir);
        Assert.Contains("|| jsonb_build_object('importacao'", ImportadorSql.Concluir);
    }
}
