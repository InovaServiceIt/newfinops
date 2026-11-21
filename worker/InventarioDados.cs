using Npgsql;
using NpgsqlTypes;

namespace CloudVista.Worker;

/// <summary>SQL do inventário (parametrizado). visto_em = coletado_em do job: coleta antiga nunca regride.</summary>
public static class InventarioSql
{
    /// <summary>Há algo desta conta visto DEPOIS desta coleta? Então ela é mais antiga que o inventário atual.</summary>
    public const string ColetaAntiga = @"
SELECT EXISTS (SELECT 1 FROM recurso WHERE cloud_conta_id = @conta AND ultimo_visto_em > @visto)
    OR EXISTS (SELECT 1 FROM compartimento WHERE cloud_conta_id = @conta AND ultimo_visto_em > @visto)";

    public const string UpsertCompartimentos = @"
INSERT INTO compartimento (cloud_conta_id, ocid, cliente_id, pai_ocid, nome, caminho, estado, criado_em_cloud, tags,
                           primeiro_visto_em, ultimo_visto_em, removido_em, job_execucao_id)
SELECT @conta, t.ocid, @cliente, t.pai, t.nome, t.caminho, t.estado, CAST(NULLIF(t.criado, '') AS timestamptz),
       CAST(t.tags AS jsonb), @visto, @visto, NULL, @job
FROM unnest(@ocids, @pais, @nomes, @caminhos, @estados, @criados, @tags) AS t(ocid, pai, nome, caminho, estado, criado, tags)
ON CONFLICT (cloud_conta_id, ocid) DO UPDATE
SET cliente_id = EXCLUDED.cliente_id, pai_ocid = EXCLUDED.pai_ocid, nome = EXCLUDED.nome, caminho = EXCLUDED.caminho,
    estado = EXCLUDED.estado, criado_em_cloud = EXCLUDED.criado_em_cloud, tags = EXCLUDED.tags,
    primeiro_visto_em = LEAST(compartimento.primeiro_visto_em, EXCLUDED.primeiro_visto_em),
    ultimo_visto_em = EXCLUDED.ultimo_visto_em, removido_em = NULL, job_execucao_id = EXCLUDED.job_execucao_id
WHERE compartimento.ultimo_visto_em <= EXCLUDED.ultimo_visto_em";

    /// <summary>
    /// Upsert com contagem de novos e reativados. Todas as CTEs veem o mesmo snapshot:
    /// "antes" tem o removido_em anterior à atualização.
    /// </summary>
    public const string UpsertRecursos = @"
WITH dados AS (
    SELECT t.chave, t.regiao, t.tipo, t.nome, t.comp, t.estado, t.criado, t.tags, t.atributos
    FROM unnest(@chaves, @regioes, @tipos, @nomes, @comps, @estados, @criados, @tags, @atributos)
        AS t(chave, regiao, tipo, nome, comp, estado, criado, tags, atributos)
), antes AS (
    SELECT r.chave, r.removido_em FROM recurso r
    WHERE r.cloud_conta_id = @conta AND r.chave = ANY(@chaves)
), gravados AS (
    INSERT INTO recurso (cloud_conta_id, chave, cliente_id, regiao, tipo, nome, compartimento_ocid, estado, criado_em_cloud,
                         tags, atributos, primeiro_visto_em, ultimo_visto_em, removido_em, job_execucao_id)
    SELECT @conta, d.chave, @cliente, d.regiao, d.tipo, d.nome, d.comp, d.estado, CAST(NULLIF(d.criado, '') AS timestamptz),
           CAST(d.tags AS jsonb), CAST(d.atributos AS jsonb), @visto, @visto, NULL, @job
    FROM dados d
    ON CONFLICT (cloud_conta_id, chave) DO UPDATE
    SET cliente_id = EXCLUDED.cliente_id, regiao = EXCLUDED.regiao, tipo = EXCLUDED.tipo, nome = EXCLUDED.nome,
        compartimento_ocid = EXCLUDED.compartimento_ocid, estado = EXCLUDED.estado, criado_em_cloud = EXCLUDED.criado_em_cloud,
        tags = EXCLUDED.tags, atributos = EXCLUDED.atributos,
        primeiro_visto_em = LEAST(recurso.primeiro_visto_em, EXCLUDED.primeiro_visto_em),
        ultimo_visto_em = EXCLUDED.ultimo_visto_em, removido_em = NULL, job_execucao_id = EXCLUDED.job_execucao_id
    WHERE recurso.ultimo_visto_em <= EXCLUDED.ultimo_visto_em
    RETURNING chave, (xmax = 0) AS novo
)
SELECT count(*) FILTER (WHERE g.novo),
       count(*) FILTER (WHERE NOT g.novo AND a.removido_em IS NOT NULL)
FROM gravados g LEFT JOIN antes a ON a.chave = g.chave";

    /// <summary>Coleta mais antiga ainda pode antecipar o primeiro_visto_em (sem mexer no resto).</summary>
    public const string AnteciparPrimeiroVisto = @"
UPDATE recurso SET primeiro_visto_em = @visto
WHERE cloud_conta_id = @conta AND chave = ANY(@chaves) AND primeiro_visto_em > @visto";

    public const string RemoverRecursos = @"
UPDATE recurso r SET removido_em = @visto
FROM unnest(@regioes, @tipos) AS p(regiao, tipo)
WHERE r.cloud_conta_id = @conta AND r.regiao = p.regiao AND r.tipo = p.tipo
  AND r.removido_em IS NULL AND r.ultimo_visto_em < @visto";

    public const string RemoverCompartimentos = @"
UPDATE compartimento SET removido_em = @visto
WHERE cloud_conta_id = @conta AND removido_em IS NULL AND ultimo_visto_em < @visto";
}

/// <summary>Grava o plano do inventário na transação da importação.</summary>
public static class InventarioGravacao
{
    public const string AvisoColetaAntiga = "coleta mais antiga que o inventário atual; remoção não aplicada";

    public static async Task<Dictionary<string, object?>> GravarAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        JobImportacao job, DateTimeOffset vistoEm, PlanoInventario plano, CancellationToken ct)
    {
        var avisos = new List<string>();
        NpgsqlCommand Cmd(string sql)
        {
            var c = new NpgsqlCommand(sql, conn, tx);
            c.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
            c.Parameters.Add(new NpgsqlParameter("visto", NpgsqlDbType.TimestampTz) { Value = vistoEm.ToUniversalTime() });
            return c;
        }
        NpgsqlParameter Arr(string nome, IEnumerable<string?> v) =>
            new(nome, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = v.ToArray() };

        bool antiga;
        await using (var c = Cmd(InventarioSql.ColetaAntiga))
            antiga = (bool)(await c.ExecuteScalarAsync(ct))!;
        if (antiga) avisos.Add(AvisoColetaAntiga);

        // 1. Compartimentos
        if (plano.Compartimentos.Count > 0)
        {
            await using var c = Cmd(InventarioSql.UpsertCompartimentos);
            c.Parameters.Add(new NpgsqlParameter("cliente", NpgsqlDbType.Bigint) { Value = job.ClienteId });
            c.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = job.Id });
            var cs = plano.Compartimentos;
            c.Parameters.Add(Arr("ocids", cs.Select(x => x.Ocid)));
            c.Parameters.Add(Arr("pais", cs.Select(x => x.PaiOcid)));
            c.Parameters.Add(Arr("nomes", cs.Select(x => x.Nome)));
            c.Parameters.Add(Arr("caminhos", cs.Select(x => x.Caminho)));
            c.Parameters.Add(Arr("estados", cs.Select(x => x.Estado)));
            c.Parameters.Add(Arr("criados", cs.Select(x => x.CriadoEmCloud)));
            c.Parameters.Add(Arr("tags", cs.Select(x => x.TagsJson)));
            await c.ExecuteNonQueryAsync(ct);
        }

        // 2. Recursos
        int novos = 0, reativados = 0;
        if (plano.Recursos.Count > 0)
        {
            var rs = plano.Recursos;
            await using (var c = Cmd(InventarioSql.UpsertRecursos))
            {
                c.Parameters.Add(new NpgsqlParameter("cliente", NpgsqlDbType.Bigint) { Value = job.ClienteId });
                c.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = job.Id });
                c.Parameters.Add(Arr("chaves", rs.Select(x => x.Chave)));
                c.Parameters.Add(Arr("regioes", rs.Select(x => x.Regiao)));
                c.Parameters.Add(Arr("tipos", rs.Select(x => x.Tipo)));
                c.Parameters.Add(Arr("nomes", rs.Select(x => x.Nome)));
                c.Parameters.Add(Arr("comps", rs.Select(x => x.CompartimentoOcid)));
                c.Parameters.Add(Arr("estados", rs.Select(x => x.Estado)));
                c.Parameters.Add(Arr("criados", rs.Select(x => x.CriadoEmCloud)));
                c.Parameters.Add(Arr("tags", rs.Select(x => x.TagsJson)));
                c.Parameters.Add(Arr("atributos", rs.Select(x => x.AtributosJson)));
                await using var r = await c.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) { novos = (int)r.GetInt64(0); reativados = (int)r.GetInt64(1); }
            }
            if (antiga)
            {
                await using var c = Cmd(InventarioSql.AnteciparPrimeiroVisto);
                c.Parameters.Add(Arr("chaves", rs.Select(x => x.Chave)));
                await c.ExecuteNonQueryAsync(ct);
            }
        }

        // 3. Remoção lógica (conservadora)
        var removidos = 0;
        if (!antiga)
        {
            if (plano.ParesRemocao.Count > 0)
            {
                await using var c = Cmd(InventarioSql.RemoverRecursos);
                c.Parameters.Add(Arr("regioes", plano.ParesRemocao.Select(p => p.Regiao)));
                c.Parameters.Add(Arr("tipos", plano.ParesRemocao.Select(p => p.Tipo)));
                removidos = await c.ExecuteNonQueryAsync(ct);
            }
            if (plano.RemoverCompartimentos)
            {
                await using var c = Cmd(InventarioSql.RemoverCompartimentos);
                var n = await c.ExecuteNonQueryAsync(ct);
                if (n > 0) avisos.Add($"{n} compartimento(s) não encontrado(s) nesta coleta marcado(s) como removido(s)");
            }
        }

        return ResumoInventario.Montar(plano, novos, removidos, reativados, avisos);
    }
}
