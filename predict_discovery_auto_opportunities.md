# 🤖 Predict Discovery — Preenchimento automático de oportunidades

> **Destinatário:** Time Antigravity  
> **Data:** Abril 2026  
> **Objetivo:** Ao clicar em "Analisar novos padrões", o sistema executa o pipeline completo e popula automaticamente a `predict_discovery_opportunities` a partir da `vw_predict_plano_acao_hostname`.

---

## Visão geral do fluxo

```
Botão "Analisar novos padrões"
        ↓
Backend chama: SELECT public.create_predict_plano_acao_hostname()
        ↓
Backend lê: vw_predict_plano_acao_hostname
        ↓
Para cada linha: INSERT em predict_discovery_opportunities (skip se já existe)
        ↓
Retorna: { inseridas: X, ignoradas: Y }
        ↓
Tela atualiza contadores "Vetorizados" e "Pendentes"
```

---

## Endpoint do backend

```
POST /api/discovery/analisar-padroes
```

**Resposta esperada:**
```json
{
  "inseridas": 42,
  "ignoradas": 156,
  "total_view": 198
}
```

---

## Lógica de implementação (C#)

```csharp
public async Task<AnalisarPadroesResult> AnalisarPadroesAsync(CancellationToken ct = default)
{
    // Passo 1: Executar o pipeline ML Tupiniquim
    await _db.ExecuteAsync(
        "SELECT public.create_predict_plano_acao_hostname()", ct);

    // Passo 2: Ler a view enriquecida
    var oportunidades = await _db.QueryAsync<VwPlanoAcaoHostname>(
        "SELECT * FROM public.vw_predict_plano_acao_hostname", ct);

    int inseridas = 0;
    int ignoradas = 0;

    // Passo 3: Popular predict_discovery_opportunities
    foreach (var item in oportunidades)
    {
        var opportunityCode = $"RAD-{item.IdPlano}";

        var existe = await _db.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (
                SELECT 1 FROM public.predict_discovery_opportunities
                WHERE client_code = @ClientCode
                AND opportunity_code = @OpportunityCode
            )",
            new { ClientCode = item.IdContrato.ToString(), OpportunityCode = opportunityCode }, ct);

        if (existe)
        {
            ignoradas++;
            continue;
        }

        await _db.ExecuteAsync(@"
            INSERT INTO public.predict_discovery_opportunities (
                client_code, opportunity_code, origin_module, origin_type,
                source_reference, radar_pattern_reference, title, summary,
                opportunity_category, priority_score, priority_rank,
                estimated_volume, volume_unit, status, created_by
            ) VALUES (
                @ClientCode, @OpportunityCode, @OriginModule, @OriginType,
                @SourceReference, @RadarPatternReference, @Title, @Summary,
                @OpportunityCategory, @PriorityScore, @PriorityRank,
                @EstimatedVolume, @VolumeUnit, @Status, @CreatedBy
            )",
            new {
                ClientCode            = item.IdContrato.ToString(),
                OpportunityCode       = opportunityCode,
                OriginModule          = "predict_radar",
                OriginType            = item.RotuloAcao,
                SourceReference       = item.NmHost,
                RadarPatternReference = item.DescricaoChave,
                Title                 = item.TituloMelhoria,
                Summary               = item.JustificativaResumida,
                OpportunityCategory   = item.CdCategoria,
                PriorityScore         = item.ScoreHostname,
                PriorityRank          = item.RankingHostname,
                EstimatedVolume       = (decimal)item.QtEventos,
                VolumeUnit            = "eventos",
                Status                = "new",
                CreatedBy             = "predict_radar"
            }, ct);

        inseridas++;
    }

    return new AnalisarPadroesResult
    {
        Inseridas  = inseridas,
        Ignoradas  = ignoradas,
        TotalView  = oportunidades.Count()
    };
}
```

---

## Mapeamento view → tabela

| Campo na `vw_predict_plano_acao_hostname` | Campo em `predict_discovery_opportunities` | Transformação |
|---|---|---|
| `id_contrato` | `client_code` | `CAST(id_contrato AS VARCHAR)` |
| `id_plano` | `opportunity_code` | `'RAD-' + id_plano` |
| `titulo_melhoria` | `title` | direto |
| `justificativa_resumida` | `summary` | direto |
| `rotulo_acao` | `origin_type` | direto |
| `nm_host` | `source_reference` | direto |
| `descricao_chave` | `radar_pattern_reference` | direto |
| `cd_categoria` | `opportunity_category` | direto |
| `score_hostname` | `priority_score` | direto (numeric) |
| `ranking_hostname` | `priority_rank` | direto (int) |
| `qt_eventos` | `estimated_volume` | cast para decimal |
| — | `volume_unit` | fixo: `'eventos'` |
| — | `origin_module` | fixo: `'predict_radar'` |
| — | `status` | fixo: `'new'` |
| — | `created_by` | fixo: `'predict_radar'` |

---

## Comportamento do botão na tela

| Momento | Estado do botão |
|---|---|
| Idle | "Analisar novos padrões" — habilitado |
| Executando | "Analisando..." — spinner, desabilitado |
| Concluído | Toast: "X novas oportunidades identificadas" |
| Erro | Toast vermelho com mensagem do erro |

---

## Atualização dos contadores após execução

Após o retorno do endpoint, atualizar os contadores do Discovery:

```
Vetorizados: total_view
Pendentes:   quantidade com status = 'new'
```

---

## SQL de validação (pós-execução)

```sql
-- Verificar oportunidades inseridas
SELECT status, COUNT(*) 
FROM public.predict_discovery_opportunities 
GROUP BY status 
ORDER BY status;

-- Verificar distribuição por cliente
SELECT client_code, COUNT(*) AS total
FROM public.predict_discovery_opportunities
WHERE status = 'new'
GROUP BY client_code
ORDER BY total DESC;
```

---

## Checklist de go-live

- [ ] Endpoint `POST /api/discovery/analisar-padroes` implementado
- [ ] Chama `create_predict_plano_acao_hostname()` antes de ler a view
- [ ] Lê `vw_predict_plano_acao_hostname` corretamente
- [ ] Insere em `predict_discovery_opportunities` com todos os campos mapeados
- [ ] Ignora registros já existentes (sem erro, sem duplicata)
- [ ] Retorna `{ inseridas, ignoradas, total_view }`
- [ ] Botão exibe spinner durante execução
- [ ] Toast de sucesso exibe quantidade de novas oportunidades
- [ ] Contadores "Vetorizados" e "Pendentes" atualizam após execução
- [ ] Testar com banco vazio — todas as linhas da view devem ser inseridas
- [ ] Testar execução dupla — segunda execução deve retornar inseridas = 0
