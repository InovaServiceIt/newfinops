using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudVista.Worker;

// Inventário OCI: compartments e resources_by_region do JSON do coletor → compartimento/recurso.
// Regras puras (sem banco); a gravação fica em InventarioRepositorio.

public sealed record CompartimentoPlano(string Ocid, string? PaiOcid, string Nome, string Caminho, string? Estado,
    string? CriadoEmCloud, string TagsJson);

public sealed record RecursoPlano(string Chave, string Regiao, string Tipo, string Nome, string? CompartimentoOcid,
    string? Estado, string? CriadoEmCloud, string TagsJson, string AtributosJson);

/// <summary>Par (região, tipo) presente como lista no JSON: candidato à remoção lógica.</summary>
public sealed record ParRemocao(string Regiao, string Tipo);

public sealed record PlanoInventario(
    IReadOnlyList<CompartimentoPlano> Compartimentos,
    IReadOnlyList<RecursoPlano> Recursos,
    IReadOnlyList<ParRemocao> ParesRemocao,
    bool RemoverCompartimentos,
    IReadOnlyList<string> Avisos)
{
    public IReadOnlyDictionary<string, int> PorTipo =>
        Recursos.GroupBy(r => r.Tipo).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
}

public static class ParserInventarioOci
{
    public const int MaxChave = 300, MaxNome = 300, MaxTipo = 60, MaxEstado = 30, MaxCaminho = 1000, MaxTexto = 200;

    /// <summary>Tipos inventariáveis (categoria → listas). Técnico, não é parâmetro de negócio.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Inventariaveis = new Dictionary<string, string[]>
    {
        ["compute"] = ["instances"],
        ["block_storage"] = ["volumes", "boot_volumes", "volume_backups", "boot_volume_backups"],
        ["object_storage"] = ["buckets"],
        ["database"] = ["db_systems", "autonomous_databases", "cloud_vm_clusters", "vm_clusters", "databases", "db_homes"],
        ["oke"] = ["clusters", "node_pools", "virtual_node_pools"],
        ["load_balancers"] = ["load_balancers", "network_load_balancers"],
        ["file_storage"] = ["file_systems", "mount_targets"],
        ["network"] = ["vcns", "subnets", "internet_gateways", "nat_gateways", "service_gateways", "drgs",
            "ip_sec_connections", "virtual_circuits", "public_ips"],
        ["security"] = ["vaults", "keys", "bastions", "certificates"],
        ["serverless"] = ["applications", "functions", "gateways", "api_deployments", "container_instances"],
        ["scheduler"] = ["schedules"],
    };

    /// <summary>Listas conhecidas que não são recurso (configuração/relacionamento): ignoradas sem aviso.</summary>
    public static readonly HashSet<string> Fora = new(StringComparer.Ordinal)
    {
        "shapes", "route_tables", "security_lists", "network_security_groups", "private_ips", "cpes",
        "remote_peering_connections", "volume_backup_policies", "volume_backup_policy_assignments",
        "capacity_reservations", "osmh_managed_instances", "worker_instance_ids", "containers", "namespace",
    };

    /// <summary>
    /// Prefixos de escopo do ErrorSink do vendor por categoria do JSON: o vendor nem sempre usa o
    /// nome da categoria (block_storage → "block.*", load_balancers → "loadbalancer.*").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> PrefixosEscopo = new Dictionary<string, string[]>
    {
        ["block_storage"] = ["block_storage", "block"],
        ["load_balancers"] = ["load_balancers", "loadbalancer"],
    };

    public const string EscopoPreflightRegiao = "preflight.region";

    /// <summary>Escopo pertence à categoria? Prefixo seguido de "." ou fim (block não casa com blockchain).</summary>
    public static bool EscopoDaCategoria(string escopo, string categoria) =>
        (PrefixosEscopo.TryGetValue(categoria, out var prefixos) ? prefixos : [categoria])
            .Any(p => escopo == p || escopo.StartsWith(p + ".", StringComparison.Ordinal));

    /// <summary>
    /// Remoção suspensa para (região, categoria): erro da categoria nessa região, erro da categoria
    /// sem região (vale para todas) ou preflight.region da região (região inalcançável).
    /// </summary>
    private static bool Suspensa(IEnumerable<ErroColeta> erros, string regiao, string categoria) =>
        erros.Any(e =>
            (e.Escopo == EscopoPreflightRegiao && e.Regiao == regiao)
            || (EscopoDaCategoria(e.Escopo, categoria) && (e.Regiao == regiao || string.IsNullOrEmpty(e.Regiao))));

    public static bool EhFora(string lista) => Fora.Contains(lista) || lista.EndsWith("_attachments", StringComparison.Ordinal);

    public static PlanoInventario Planejar(JsonElement raiz)
    {
        var avisos = new List<string>();
        var erros = LerErros(raiz);

        // ── Compartimentos ──
        var compartimentos = new List<CompartimentoPlano>();
        var removerCompartimentos = false;
        if (raiz.TryGetProperty("compartments", out var comps) && comps.ValueKind == JsonValueKind.Array && comps.GetArrayLength() > 0)
        {
            compartimentos = PlanejarCompartimentos(comps, avisos);
            var erroIam = erros.Any(e => EscopoDaCategoria(e.Escopo, "compartments") || EscopoDaCategoria(e.Escopo, "iam"));
            removerCompartimentos = !erroIam;
            if (erroIam) avisos.Add("remoção suspensa: compartimentos (erro na coleta de IAM/compartments)");
        }

        // ── Recursos ──
        var recursos = new Dictionary<string, RecursoPlano>(StringComparer.Ordinal);
        var pares = new List<ParRemocao>();
        var desconhecidos = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var semId = 0;
        if (raiz.TryGetProperty("resources_by_region", out var regioes) && regioes.ValueKind == JsonValueKind.Object)
        {
            foreach (var regiao in regioes.EnumerateObject())
            {
                if (regiao.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var categoria in regiao.Value.EnumerateObject())
                {
                    if (categoria.Value.ValueKind != JsonValueKind.Object) continue;
                    var cat = categoria.Name;
                    var anexados = cat == "block_storage" ? Anexados(regiao.Value) : null;
                    var namespaceCategoria = Texto(categoria.Value, "namespace");
                    var suspensa = Suspensa(erros, regiao.Name, cat);
                    var avisouSuspensao = false;

                    foreach (var lista in categoria.Value.EnumerateObject())
                    {
                        if (lista.Value.ValueKind != JsonValueKind.Array) continue;
                        var tipo = $"{cat}.{lista.Name}";
                        if (!Inventariaveis.TryGetValue(cat, out var listas) || !listas.Contains(lista.Name))
                        {
                            if (!EhFora(lista.Name) && lista.Value.GetArrayLength() > 0)
                                desconhecidos[tipo] = desconhecidos.GetValueOrDefault(tipo) + lista.Value.GetArrayLength();
                            continue;
                        }

                        if (suspensa)
                        {
                            if (!avisouSuspensao) avisos.Add($"remoção suspensa: {cat} em {regiao.Name} (erro na coleta)");
                            avisouSuspensao = true;
                        }
                        else
                        {
                            pares.Add(new ParRemocao(regiao.Name, tipo));
                        }

                        foreach (var item in lista.Value.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            var r = PlanejarRecurso(item, regiao.Name, tipo, namespaceCategoria, anexados);
                            if (r is null) { semId++; continue; }
                            recursos[r.Chave] = r; // repetido (mesma chave): vale o último
                        }
                    }
                }
            }
        }

        foreach (var (tipo, n) in desconhecidos)
            avisos.Add($"tipo não inventariado: {tipo} ({n})");
        if (semId > 0)
            avisos.Add($"{semId} item(ns) sem identificador válido ignorado(s)");

        return new PlanoInventario(compartimentos, recursos.Values.ToList(), pares, removerCompartimentos, avisos);
    }

    // ── compartimentos ──────────────────────────────────────────────────────

    private static List<CompartimentoPlano> PlanejarCompartimentos(JsonElement comps, List<string> avisos)
    {
        var porId = new Dictionary<string, (string Nome, string? Pai, JsonElement El)>(StringComparer.Ordinal);
        foreach (var c in comps.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object || Texto(c, "id") is not { Length: > 0 and <= MaxChave } id) continue;
            porId[id] = (Texto(c, "name") ?? id, Texto(c, "compartment_id"), c);
        }

        var parciais = 0;
        var lista = new List<CompartimentoPlano>();
        foreach (var (id, (nome, pai, el)) in porId)
        {
            var partes = new List<string> { nome };
            var visto = new HashSet<string>(StringComparer.Ordinal) { id };
            var atual = pai;
            var parcial = false;
            while (!string.IsNullOrEmpty(atual) && visto.Add(atual))
            {
                if (!porId.TryGetValue(atual, out var p)) { parcial = true; break; }
                partes.Add(p.Nome);
                atual = p.Pai;
            }
            if (parcial) parciais++;
            partes.Reverse();
            lista.Add(new CompartimentoPlano(id, string.IsNullOrEmpty(pai) ? null : pai, Cortar(nome, MaxNome)!,
                Cortar(string.Join('/', partes), MaxCaminho)!, Cortar(Texto(el, "lifecycle_state"), MaxEstado),
                Texto(el, "time_created"), Tags(el)));
        }
        if (parciais > 0)
            avisos.Add($"{parciais} compartimento(s) com pai ausente na coleta: caminho parcial");
        return lista;
    }

    // ── recursos ────────────────────────────────────────────────────────────

    public static RecursoPlano? PlanejarRecurso(JsonElement item, string regiao, string tipo, string? namespaceCategoria = null,
        IReadOnlySet<string>? anexados = null)
    {
        string? chave;
        if (tipo == "object_storage.buckets")
        {
            var ns = Texto(item, "namespace") ?? namespaceCategoria;
            var nomeBucket = Texto(item, "name");
            chave = string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(nomeBucket) ? null : $"bucket:{ns}/{nomeBucket}";
        }
        else
        {
            chave = Texto(item, "id");
        }
        if (string.IsNullOrEmpty(chave) || chave.Length > MaxChave || tipo.Length > MaxTipo) return null;

        var nome = Texto(item, "display_name") ?? Texto(item, "name") ?? FimDoOcid(chave);
        return new RecursoPlano(chave, regiao, tipo, Cortar(nome, MaxNome)!, Texto(item, "compartment_id"),
            Cortar(Texto(item, "lifecycle_state"), MaxEstado), Texto(item, "time_created"), Tags(item),
            JsonSerializer.Serialize(Atributos(item, tipo, anexados)));
    }

    /// <summary>Lista branca de atributos por tipo (só escalares; strings ≤200). Nunca o objeto inteiro.</summary>
    public static SortedDictionary<string, object> Atributos(JsonElement item, string tipo, IReadOnlySet<string>? anexados = null)
    {
        var a = new SortedDictionary<string, object>(StringComparer.Ordinal);
        void Copiar(params string[] campos)
        {
            foreach (var c in campos)
                if (Escalar(item, c) is { } v) a[c] = v;
        }

        var (cat, lista) = (tipo.Split('.')[0], tipo[(tipo.IndexOf('.') + 1)..]);
        switch (cat)
        {
            case "compute" when lista == "instances":
                Copiar("shape", "availability_domain", "fault_domain", "launch_mode");
                if (item.TryGetProperty("shape_config", out var sc) && sc.ValueKind == JsonValueKind.Object)
                {
                    if (Escalar(sc, "ocpus") is { } o) a["ocpus"] = o;
                    if (Escalar(sc, "memory_in_gbs") is { } m) a["memory_in_gbs"] = m;
                }
                break;
            case "block_storage" when lista is "volumes" or "boot_volumes":
                Copiar("size_in_gbs", "vpus_per_gb", "availability_domain");
                if (anexados is not null && Texto(item, "id") is { } vid) a["anexado"] = anexados.Contains(vid);
                break;
            case "block_storage":
                Copiar("size_in_gbs", "unique_size_in_gbs", "type", "source_type", "expiration_time");
                break;
            case "object_storage":
                Copiar("storage_tier", "public_access_type", "versioning", "approximate_size", "approximate_count");
                break;
            case "database":
                Copiar("db_workload", "cpu_core_count", "compute_count", "compute_model", "data_storage_size_in_tbs",
                    "data_storage_size_in_gbs", "license_model", "is_free_tier", "db_version");
                break;
            case "oke":
                Copiar("kubernetes_version", "node_shape", "node_count");
                if (!a.ContainsKey("node_count") && item.TryGetProperty("node_config_details", out var nc)
                    && nc.ValueKind == JsonValueKind.Object && Escalar(nc, "size") is { } n)
                    a["node_count"] = n;
                break;
            case "load_balancers":
                Copiar("shape_name", "is_private");
                if (item.TryGetProperty("ip_addresses", out var ips) && ips.ValueKind == JsonValueKind.Array)
                    foreach (var ip in ips.EnumerateArray())
                        if (ip.ValueKind == JsonValueKind.Object && Escalar(ip, "ip_address") is { } addr) { a["ip_address"] = addr; break; }
                break;
            case "network" when lista == "public_ips":
                Copiar("lifetime", "ip_address", "assigned_entity_type");
                a["atribuido"] = !string.IsNullOrEmpty(Texto(item, "assigned_entity_id"));
                break;
            case "network" when lista is "vcns" or "subnets":
                Copiar("cidr_block");
                break;
            case "security" when lista == "certificates":
                Copiar("config_type", "not_after");
                if (!a.ContainsKey("not_after") && Caminho(item, "current_version_summary", "validity", "time_of_validity_not_after") is { } na)
                    a["not_after"] = na;
                break;
            case "security" when lista == "keys":
                Copiar("protection_mode", "algorithm");
                break;
            case "security" when lista == "vaults":
                Copiar("vault_type");
                break;
        }
        return a;
    }

    /// <summary>OCIDs de volumes/boot volumes com anexo ATTACHED na região (attachments ficam em compute).</summary>
    public static HashSet<string> Anexados(JsonElement regiao)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!regiao.TryGetProperty("compute", out var compute) || compute.ValueKind != JsonValueKind.Object) return ids;
        foreach (var (lista, campo) in new[] { ("volume_attachments", "volume_id"), ("boot_volume_attachments", "boot_volume_id") })
        {
            if (!compute.TryGetProperty(lista, out var att) || att.ValueKind != JsonValueKind.Array) continue;
            foreach (var x in att.EnumerateArray())
                if (x.ValueKind == JsonValueKind.Object && Texto(x, "lifecycle_state") == "ATTACHED" && Texto(x, campo) is { } id)
                    ids.Add(id);
        }
        return ids;
    }

    // ── auxiliares ──────────────────────────────────────────────────────────

    private sealed record ErroColeta(string Escopo, string? Regiao);

    private static List<ErroColeta> LerErros(JsonElement raiz)
    {
        var lista = new List<ErroColeta>();
        if (raiz.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Array)
            foreach (var e in erros.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object)
                    lista.Add(new ErroColeta(Texto(e, "scope") ?? "", Texto(e, "region")));
        return lista;
    }

    private static string Tags(JsonElement item)
    {
        JsonNode Obj(string campo) =>
            item.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.Object ? JsonNode.Parse(v.GetRawText())! : new JsonObject();
        return new JsonObject { ["freeform"] = Obj("freeform_tags"), ["defined"] = Obj("defined_tags") }.ToJsonString();
    }

    public static string? Texto(JsonElement el, string campo) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static object? Escalar(JsonElement el, string campo)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(campo, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => Cortar(v.GetString(), MaxTexto),
            JsonValueKind.Number => v.TryGetInt64(out var l) ? l : v.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static object? Caminho(JsonElement el, params string[] campos)
    {
        var atual = el;
        for (var i = 0; i < campos.Length - 1; i++)
            if (atual.ValueKind != JsonValueKind.Object || !atual.TryGetProperty(campos[i], out atual)) return null;
        return Escalar(atual, campos[^1]);
    }

    private static string FimDoOcid(string chave)
    {
        var fim = chave[(chave.LastIndexOf('.') + 1)..];
        return fim.Length > 12 ? fim[^12..] : fim;
    }

    private static string? Cortar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>Resumo do inventário mesclado em resumo.importacao.inventario.</summary>
public static class ResumoInventario
{
    public static Dictionary<string, object?> Montar(PlanoInventario plano, int novos, int removidos, int reativados, IEnumerable<string> avisosExtras) => new()
    {
        ["compartimentos"] = plano.Compartimentos.Count,
        ["recursos"] = new Dictionary<string, object?> { ["total"] = plano.Recursos.Count, ["por_tipo"] = plano.PorTipo },
        ["novos"] = novos,
        ["removidos"] = removidos,
        ["reativados"] = reativados,
        ["avisos"] = plano.Avisos.Concat(avisosExtras).ToList(),
    };

    public static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
}
