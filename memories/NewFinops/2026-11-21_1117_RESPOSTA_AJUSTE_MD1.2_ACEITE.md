# Resposta — ajuste pós-aceite MD 1.2/1.3

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 21/11/2026 11:17 (Brasília)

**Commit:** `03531ac` no `main`. Sem DDL e sem acesso ao banco desta máquina.

## 1. Duração após reimportar
- **Antes e depois:** `ColetasSql.ListarColetas` calculava a duração como `COALESCE(finalizado_em, now()) − iniciado_em`. Agora calcula como **`COALESCE(coletado_em, finalizado_em, now()) − iniciado_em`**, ou seja, o tempo do coletor.
- **Quando `coletado_em` é nulo** (teste de credencial, ou erro antes de coletar), vale a regra anterior.
- **Efeito no job #4:** volta a mostrar ~7–8 min, porque a reimportação mexe em `finalizado_em` e não em `coletado_em`.
- **Teste:** `AjusteAceiteTests.Duracao_da_coleta_usa_coletado_em_e_nao_muda_ao_reimportar`.

## 2. Sinal "Sem tag (exceto automáticas)"
- **Nome:** o sinal mudou de `sem_tag_defined` para **`sem_tag`**, com o rótulo **"Sem tag (exceto automáticas)"**. O tooltip explica a regra.
- **Regra única:** novo `backend/Inventario/TagsSql.cs` com `Automatica` (`(ns = 'Oracle-Tags' OR ns LIKE '%-Oracle-Tags') AND chave IN ('CreatedBy','CreatedOn')`) e `Namespace`.
  - A **Governança** (`pares`) passou a usar essas mesmas constantes.
  - O **sinal** usa `NOT TagsSql.TemTagNaoAutomaticaR`, que é verdadeiro quando o recurso não tem nenhuma tag defined não automática **e** nenhuma freeform.
- **Por que bate com a Governança:** é a definição exata do "com alguma tag" da Governança com o toggle desligado (`pares` = defined não automáticas ∪ freeform). Assim, "sem tag" no Inventário = "sem nenhuma tag" na Governança para o mesmo filtro (cliente, conta, região, tipo, compartimento; recursos ativos).
- **Caso do aceite:** o volume `BV_MTRDEVSAPXD0_U02_01`, que só tem `Oracle-Tags.CreatedBy/CreatedOn`, agora entra no sinal.
- **Testes:** `AjusteAceiteTests` cobre:
  - o sinal usando a mesma condição de automáticas que a Governança, sem texto duplicado;
  - olhar defined e freeform;
  - o alias novo no KPI;
  - os casos do aceite em `EhAutomatica`.

  O "teste cruzado" com números reais fica para o HLGDR (sem banco aqui): os dois valores devem coincidir.

## 3. Validação
- `dotnet test`: **340 aprovados**. Frontend: `typecheck`, `build` e `lint:colors` verdes.

Sigo agora com o MD 1.5.
