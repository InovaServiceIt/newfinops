using System.Text.Json;
using CloudVista.Worker;

namespace Finops.Backend.Tests;

/// <summary>
/// Fixture sintética com o MESMO formato do JSON do coletor do Maurício:
/// compartments (árvore achatada) e resources_by_region.&lt;regiao&gt;.&lt;categoria&gt;.&lt;lista&gt;.
/// </summary>
public class InventarioTests
{
    private const string Fixture = """
    {
      "compartments": [
        {"id": "ocid1.tenancy.oc1..raiz", "name": "metrorio", "compartment_id": null, "lifecycle_state": "ACTIVE", "is_root": true},
        {"id": "ocid1.compartment.oc1..prod", "name": "Producao", "compartment_id": "ocid1.tenancy.oc1..raiz", "lifecycle_state": "ACTIVE",
         "time_created": "2024-01-02T03:04:05+00:00", "freeform_tags": {"area": "ti"}, "defined_tags": {"Custo": {"cc": "100"}}},
        {"id": "ocid1.compartment.oc1..app", "name": "App", "compartment_id": "ocid1.compartment.oc1..prod", "lifecycle_state": "ACTIVE"},
        {"id": "ocid1.compartment.oc1..orfao", "name": "Orfao", "compartment_id": "ocid1.compartment.oc1..sumiu", "lifecycle_state": "ACTIVE"}
      ],
      "resources_by_region": {
        "sa-saopaulo-1": {
          "compute": {
            "instances": [
              {"id": "ocid1.instance.oc1..vm1", "display_name": "vm-01", "compartment_id": "ocid1.compartment.oc1..app",
               "lifecycle_state": "STOPPED", "time_created": "2025-05-05T10:00:00+00:00", "shape": "VM.Standard.E4.Flex",
               "shape_config": {"ocpus": 2.0, "memory_in_gbs": 16, "gpus": 0}, "availability_domain": "AD-1",
               "metadata": {"ssh_authorized_keys": "ssh-rsa AAAA-segredo"}, "extended_metadata": {"x": 1},
               "freeform_tags": {"dono": "joao"}}
            ],
            "shapes": [{"shape": "VM.Standard.E4.Flex"}],
            "volume_attachments": [
              {"id": "ocid1.volumeattachment.oc1..a1", "volume_id": "ocid1.volume.oc1..v1", "lifecycle_state": "ATTACHED"},
              {"id": "ocid1.volumeattachment.oc1..a2", "volume_id": "ocid1.volume.oc1..v2", "lifecycle_state": "DETACHED"}
            ],
            "boot_volume_attachments": [
              {"id": "ocid1.bootvolumeattachment.oc1..b1", "boot_volume_id": "ocid1.bootvolume.oc1..bv1", "lifecycle_state": "ATTACHED"}
            ],
            "images_catalog": [{"id": "ocid1.image.oc1..img"}]
          },
          "block_storage": {
            "volumes": [
              {"id": "ocid1.volume.oc1..v1", "display_name": "dados", "size_in_gbs": 100, "vpus_per_gb": 10, "lifecycle_state": "AVAILABLE"},
              {"id": "ocid1.volume.oc1..v2", "display_name": "solto", "size_in_gbs": 50, "lifecycle_state": "AVAILABLE"},
              {"id": "ocid1.volume.oc1..v1", "display_name": "dados-renomeado", "size_in_gbs": 100}
            ],
            "boot_volumes": [{"id": "ocid1.bootvolume.oc1..bv1", "size_in_gbs": 47}],
            "volume_backups": [{"id": "ocid1.volumebackup.oc1..bk1", "size_in_gbs": 100, "unique_size_in_gbs": 3, "type": "FULL",
                                "source_type": "MANUAL", "time_created": "2024-01-01T00:00:00+00:00"}],
            "boot_volume_backups": [],
            "volume_backup_policies": [{"id": "ocid1.volumebackuppolicy.oc1..p"}]
          },
          "object_storage": {
            "namespace": "nsmetro",
            "buckets": [
              {"name": "logs", "namespace": "nsmetro", "compartment_id": "ocid1.compartment.oc1..prod", "storage_tier": "Standard",
               "public_access_type": "NoPublicAccess", "approximate_size": 123456, "approximate_count": 10, "etag": "abc",
               "lifecycle_rules": [{"name": "x"}]},
              {"name": "sem-ns"}
            ]
          },
          "network": {
            "vcns": [{"id": "ocid1.vcn.oc1..n1", "display_name": "vcn-prod", "cidr_block": "10.0.0.0/16", "cidr_blocks": ["10.0.0.0/16"]}],
            "public_ips": [
              {"id": "ocid1.publicip.oc1..ip1", "ip_address": "200.1.1.1", "lifetime": "RESERVED", "assigned_entity_id": null},
              {"id": "ocid1.publicip.oc1..ip2", "ip_address": "200.1.1.2", "lifetime": "RESERVED", "assigned_entity_id": "ocid1.privateip.oc1..x",
               "assigned_entity_type": "PRIVATE_IP"}
            ],
            "route_tables": [{"id": "ocid1.routetable.oc1..r"}],
            "private_ips": [{"id": "ocid1.privateip.oc1..x"}]
          },
          "security": {
            "certificates": [{"id": "ocid1.certificate.oc1..c1", "name": "site", "config_type": "IMPORTED",
              "current_version_summary": {"validity": {"time_of_validity_not_after": "2026-12-01T00:00:00+00:00"}},
              "certificate_pem": "-----BEGIN CERTIFICATE-----xyz"}],
            "keys": [{"id": "ocid1.key.oc1..k1", "display_name": "chave", "protection_mode": "HSM", "algorithm": "AES"}]
          },
          "load_balancers": {
            "load_balancers": [{"id": "ocid1.loadbalancer.oc1..lb1", "display_name": "lb", "shape_name": "flexible", "is_private": false,
              "ip_addresses": [{"ip_address": "200.2.2.2", "is_public": true}, {"ip_address": "200.2.2.3"}]}]
          },
          "oke": {
            "node_pools": [{"id": "ocid1.nodepool.oc1..np", "name": "pool", "kubernetes_version": "v1.29", "node_shape": "VM.E4",
              "node_config_details": {"size": 3}}]
          }
        },
        "sa-vinhedo-1": {
          "compute": { "instances": [{"id": "ocid1.instance.oc1..vm2", "lifecycle_state": "RUNNING"}] },
          "block_storage": { "volumes": [] }
        }
      },
      "errors": [
        {"scope": "block.volumes", "region": "sa-vinhedo-1", "compartment_id": "x", "error_type": "ServiceError", "error_code": "500", "message": "falha"}
      ]
    }
    """;

    private static PlanoInventario Plano(string json = Fixture)
    {
        using var doc = JsonDocument.Parse(json);
        return ParserInventarioOci.Planejar(doc.RootElement);
    }

    private static RecursoPlano R(PlanoInventario p, string chave) => p.Recursos.Single(r => r.Chave == chave);
    private static JsonElement Attr(RecursoPlano r) => JsonDocument.Parse(r.AtributosJson).RootElement;

    [Fact]
    public void Extrai_so_tipos_da_lista_branca()
    {
        var p = Plano();
        Assert.Equal(new Dictionary<string, int>
        {
            ["block_storage.boot_volumes"] = 1, ["block_storage.volume_backups"] = 1, ["block_storage.volumes"] = 2,
            ["compute.instances"] = 2, ["load_balancers.load_balancers"] = 1, ["network.public_ips"] = 2, ["network.vcns"] = 1,
            ["object_storage.buckets"] = 2, ["oke.node_pools"] = 1, ["security.certificates"] = 1, ["security.keys"] = 1,
        }, p.PorTipo);
        Assert.DoesNotContain(p.Recursos, r => r.Tipo.Contains("attachments") || r.Tipo.EndsWith("shapes") || r.Tipo.EndsWith("route_tables"));
    }

    [Fact]
    public void Tipo_desconhecido_vira_aviso_e_fora_da_lista_nao()
    {
        var p = Plano();
        Assert.Contains("tipo não inventariado: compute.images_catalog (1)", p.Avisos);
        Assert.DoesNotContain(p.Avisos, a => a.Contains("route_tables") || a.Contains("volume_attachments") || a.Contains("shapes"));
    }

    [Fact]
    public void Atributos_so_da_lista_branca()
    {
        var vm = R(Plano(), "ocid1.instance.oc1..vm1");
        var a = Attr(vm);
        Assert.Equal("VM.Standard.E4.Flex", a.GetProperty("shape").GetString());
        Assert.Equal(2.0m, a.GetProperty("ocpus").GetDecimal());
        Assert.Equal(16, a.GetProperty("memory_in_gbs").GetInt32());
        Assert.False(a.TryGetProperty("metadata", out _));
        Assert.False(a.TryGetProperty("gpus", out _));
        Assert.DoesNotContain("segredo", vm.AtributosJson);

        var cert = R(Plano(), "ocid1.certificate.oc1..c1");
        Assert.Equal("2026-12-01T00:00:00+00:00", Attr(cert).GetProperty("not_after").GetString());
        Assert.DoesNotContain("BEGIN", cert.AtributosJson);

        var bucket = R(Plano(), "bucket:nsmetro/logs");
        Assert.False(Attr(bucket).TryGetProperty("etag", out _));
        Assert.False(Attr(bucket).TryGetProperty("lifecycle_rules", out _));
        Assert.Equal(123456, Attr(bucket).GetProperty("approximate_size").GetInt64());
    }

    [Fact]
    public void Anexado_e_atribuido()
    {
        var p = Plano();
        Assert.True(Attr(R(p, "ocid1.volume.oc1..v1")).GetProperty("anexado").GetBoolean());
        Assert.False(Attr(R(p, "ocid1.volume.oc1..v2")).GetProperty("anexado").GetBoolean());   // DETACHED
        Assert.True(Attr(R(p, "ocid1.bootvolume.oc1..bv1")).GetProperty("anexado").GetBoolean());
        Assert.False(Attr(R(p, "ocid1.publicip.oc1..ip1")).GetProperty("atribuido").GetBoolean());
        Assert.True(Attr(R(p, "ocid1.publicip.oc1..ip2")).GetProperty("atribuido").GetBoolean());
    }

    [Fact]
    public void Outros_atributos_por_tipo()
    {
        var p = Plano();
        Assert.Equal("200.2.2.2", Attr(R(p, "ocid1.loadbalancer.oc1..lb1")).GetProperty("ip_address").GetString());
        Assert.Equal(3, Attr(R(p, "ocid1.nodepool.oc1..np")).GetProperty("node_count").GetInt32());
        Assert.Equal("10.0.0.0/16", Attr(R(p, "ocid1.vcn.oc1..n1")).GetProperty("cidr_block").GetString());
        Assert.False(Attr(R(p, "ocid1.vcn.oc1..n1")).TryGetProperty("cidr_blocks", out _));
        Assert.Equal("HSM", Attr(R(p, "ocid1.key.oc1..k1")).GetProperty("protection_mode").GetString());
    }

    [Fact]
    public void Chave_de_bucket_e_nome()
    {
        var p = Plano();
        var b = R(p, "bucket:nsmetro/logs");
        Assert.Equal(("logs", "ocid1.compartment.oc1..prod"), (b.Nome, b.CompartimentoOcid));
        // bucket sem namespace no item usa o namespace da categoria
        Assert.Contains(p.Recursos, r => r.Chave == "bucket:nsmetro/sem-ns");
        // sem display_name nem name: fim do OCID
        Assert.Equal("vm2", R(p, "ocid1.instance.oc1..vm2").Nome);
    }

    [Fact]
    public void Chave_repetida_vale_a_ultima()
    {
        Assert.Equal("dados-renomeado", R(Plano(), "ocid1.volume.oc1..v1").Nome);
    }

    [Fact]
    public void Tags_freeform_e_defined()
    {
        var tags = JsonDocument.Parse(R(Plano(), "ocid1.instance.oc1..vm1").TagsJson).RootElement;
        Assert.Equal("joao", tags.GetProperty("freeform").GetProperty("dono").GetString());
        Assert.Equal(JsonValueKind.Object, tags.GetProperty("defined").ValueKind);
    }

    [Fact]
    public void Caminho_de_compartimentos_e_pai_ausente()
    {
        var p = Plano();
        var app = p.Compartimentos.Single(c => c.Ocid == "ocid1.compartment.oc1..app");
        Assert.Equal(("metrorio/Producao/App", "ocid1.compartment.oc1..prod"), (app.Caminho, app.PaiOcid));
        Assert.Null(p.Compartimentos.Single(c => c.Ocid == "ocid1.tenancy.oc1..raiz").PaiOcid);
        Assert.Equal("Orfao", p.Compartimentos.Single(c => c.Ocid == "ocid1.compartment.oc1..orfao").Caminho);
        Assert.Contains("1 compartimento(s) com pai ausente na coleta: caminho parcial", p.Avisos);
        Assert.Contains("\"cc\":\"100\"", p.Compartimentos.Single(c => c.Nome == "Producao").TagsJson);
    }

    [Fact]
    public void Remocao_suspensa_pela_categoria_e_regiao_com_erro()
    {
        var p = Plano();
        Assert.Contains(new ParRemocao("sa-vinhedo-1", "compute.instances"), p.ParesRemocao);
        Assert.DoesNotContain(new ParRemocao("sa-vinhedo-1", "block_storage.volumes"), p.ParesRemocao);
        Assert.Contains(new ParRemocao("sa-saopaulo-1", "block_storage.volumes"), p.ParesRemocao);   // erro é só em vinhedo
        Assert.Contains(new ParRemocao("sa-saopaulo-1", "block_storage.boot_volume_backups"), p.ParesRemocao); // lista vazia presente
        Assert.Contains("remoção suspensa: block_storage em sa-vinhedo-1 (erro na coleta)", p.Avisos);
        Assert.True(p.RemoverCompartimentos);
    }

    private const string Regioes2 = """
      "resources_by_region": {
        "sa-saopaulo-1": { "block_storage": { "volumes": [] }, "load_balancers": { "load_balancers": [] }, "compute": { "instances": [] } },
        "sa-vinhedo-1":  { "block_storage": { "volumes": [] }, "load_balancers": { "load_balancers": [] }, "compute": { "instances": [] } }
      }
    """;

    private static PlanoInventario ComErros(string erros) => Plano("{" + Regioes2 + ", \"errors\": [" + erros + "] }");

    [Theory]
    [InlineData("block.volumes", "block_storage.volumes")]
    [InlineData("block.boot_volume_backups", "block_storage.volumes")]
    [InlineData("block_storage", "block_storage.volumes")]
    [InlineData("loadbalancer.load_balancers", "load_balancers.load_balancers")]
    [InlineData("compute", "compute.instances")]
    public void Escopo_do_vendor_suspende_a_categoria_certa(string escopo, string tipo)
    {
        var p = ComErros($$"""{"scope": "{{escopo}}", "region": "sa-vinhedo-1"}""");
        Assert.DoesNotContain(new ParRemocao("sa-vinhedo-1", tipo), p.ParesRemocao);
        Assert.Contains(new ParRemocao("sa-saopaulo-1", tipo), p.ParesRemocao);
    }

    [Fact]
    public void Prefixo_exige_ponto_ou_fim()
    {
        Assert.False(ParserInventarioOci.EscopoDaCategoria("blockchain.x", "block_storage"));
        Assert.True(ParserInventarioOci.EscopoDaCategoria("block", "block_storage"));
        Assert.False(ParserInventarioOci.EscopoDaCategoria("computex", "compute"));
        var p = ComErros("""{"scope": "blockchain.x", "region": "sa-vinhedo-1"}""");
        Assert.Contains(new ParRemocao("sa-vinhedo-1", "block_storage.volumes"), p.ParesRemocao);
    }

    [Fact]
    public void Preflight_da_regiao_suspende_a_regiao_inteira()
    {
        var p = ComErros("""{"scope": "preflight.region", "region": "sa-vinhedo-1"}""");
        Assert.DoesNotContain(p.ParesRemocao, x => x.Regiao == "sa-vinhedo-1");
        Assert.Equal(3, p.ParesRemocao.Count(x => x.Regiao == "sa-saopaulo-1"));
    }

    [Fact]
    public void Erro_sem_regiao_suspende_a_categoria_em_todas_as_regioes()
    {
        var p = ComErros("""{"scope": "loadbalancer.list", "region": null}""");
        Assert.DoesNotContain(p.ParesRemocao, x => x.Tipo == "load_balancers.load_balancers");
        Assert.Contains(new ParRemocao("sa-vinhedo-1", "compute.instances"), p.ParesRemocao);
    }

    [Fact]
    public void Lista_ausente_nao_entra_na_remocao()
    {
        var p = Plano();
        Assert.DoesNotContain(p.ParesRemocao, x => x.Regiao == "sa-vinhedo-1" && x.Tipo.StartsWith("network"));
    }

    [Fact]
    public void Erro_de_iam_suspende_remocao_de_compartimentos()
    {
        var p = Plano("""
        { "compartments": [{"id": "ocid1.tenancy.oc1..raiz", "name": "r"}],
          "errors": [{"scope": "iam.policies", "region": "sa-saopaulo-1"}] }
        """);
        Assert.False(p.RemoverCompartimentos);
        Assert.Contains(p.Avisos, a => a.StartsWith("remoção suspensa: compartimentos"));
    }

    [Fact]
    public void Sem_compartments_nao_remove_compartimentos()
    {
        Assert.False(Plano("""{ "resources_by_region": {} }""").RemoverCompartimentos);
    }

    [Fact]
    public void Resumo_do_inventario()
    {
        var p = Plano();
        var r = ResumoInventario.Montar(p, novos: 3, removidos: 1, reativados: 2, ["x"]);
        Assert.Equal(4, r["compartimentos"]);
        Assert.Equal(15, ((Dictionary<string, object?>)r["recursos"]!)["total"]);
        Assert.Equal((3, 1, 2), ((int)r["novos"]!, (int)r["removidos"]!, (int)r["reativados"]!));
        Assert.Contains("x", (List<string>)r["avisos"]!);
    }

    [Fact]
    public void Importa_inventario_so_em_inventario_e_completa()
    {
        Assert.True(ImportadorRepositorio.ImportaInventario("COLETA_INVENTARIO"));
        Assert.True(ImportadorRepositorio.ImportaInventario("COLETA_COMPLETA"));
        Assert.False(ImportadorRepositorio.ImportaInventario("COLETA_CUSTO"));
    }

    [Fact]
    public void Sql_nao_regride_e_remocao_e_logica()
    {
        Assert.Contains("WHERE recurso.ultimo_visto_em <= EXCLUDED.ultimo_visto_em", InventarioSql.UpsertRecursos);
        Assert.Contains("LEAST(recurso.primeiro_visto_em, EXCLUDED.primeiro_visto_em)", InventarioSql.UpsertRecursos);
        Assert.Contains("removido_em = NULL", InventarioSql.UpsertRecursos);
        Assert.Contains("WHERE compartimento.ultimo_visto_em <= EXCLUDED.ultimo_visto_em", InventarioSql.UpsertCompartimentos);
        Assert.Contains("r.ultimo_visto_em < @visto", InventarioSql.RemoverRecursos);
        foreach (var sql in new[] { InventarioSql.UpsertRecursos, InventarioSql.RemoverRecursos, InventarioSql.RemoverCompartimentos })
        {
            Assert.DoesNotContain("DELETE", sql);
            Assert.DoesNotContain("*", sql.Replace("count(*)", ""));
        }
        Assert.Contains("coletado_em", ImportadorSql.Claim);
    }
}
