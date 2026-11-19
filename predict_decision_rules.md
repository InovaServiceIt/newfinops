# Predict - CRUD de Decision Rules

## Objetivo
Permitir o cadastro, consulta, edição, visualização e inativação das regras de decisão do Predict, responsáveis por avaliar eventos normalizados e determinar o comportamento operacional esperado, como ignorar evento, monitorar, abrir incidente ou encerrar evento.

---

## Nome da funcionalidade
**Decision Rules**

## Nome técnico sugerido
**predict_decision_rules**

## Objetivo da entidade
Armazenar as regras de decisão aplicadas pelo motor do Predict Decision v2, permitindo classificar eventos conforme atributos de origem, categoria, severidade, frequência, conteúdo textual e contexto operacional.

---

## Diretriz funcional
As regras deverão ser avaliadas por ordem de prioridade.  
A primeira regra ativa e aderente ao evento deverá ser considerada vencedora no processo de decisão.

---

## Estrutura da tela

A tela deverá seguir o padrão:
1. Cabeçalho
2. Área de filtros
3. Grid de listagem
4. Formulário lateral de inclusão/edição/visualização
5. Modal de confirmação para inativação

---

## Cabeçalho da página

### Título
**Decision Rules**

### Subtítulo
Cadastro e manutenção das regras que orientam o motor de decisão do Predict.

### Botão principal
**Nova Regra**

---

## Área de filtros

### Objetivo
Permitir localização rápida de regras por identificação, contexto técnico e status.

### Campos de filtro
1. Código da regra
2. Nome da regra
3. Origem do evento
4. Categoria
5. Subcategoria
6. Ambiente
7. Serviço
8. Status da decisão
9. Ativa
10. Abre incidente
11. Fecha evento

### Botões
- **Pesquisar**
- **Limpar**

### Comportamento
- Os filtros devem aceitar combinação.
- Campo de nome deve aceitar busca parcial.
- Combos devem permitir seleção única.
- Campo “Ativa” deve aceitar:
  - Todos
  - Sim
  - Não

---

## Grid de listagem

### Objetivo
Exibir as regras cadastradas de forma clara e operacional.

### Colunas sugeridas
1. Prioridade
2. Código da Regra
3. Nome da Regra
4. Origem
5. Categoria
6. Subcategoria
7. Ambiente
8. Serviço
9. Severidade
10. Janela (min)
11. Decision Status
12. Abre Incidente
13. Fecha Evento
14. Ativa
15. Alterado em
16. Ações

### Regras de exibição
- Severidade deve ser exibida como faixa:
  - Ex.: `2 a 4`
- Janela deve exibir minutos
- Booleanos devem aparecer como:
  - Sim / Não
- Regras inativas devem ter aparência visual discreta
- Ordenação padrão inicial:
  - `priority_order ASC`

### Ações por linha
- Visualizar
- Editar
- Ativar/Inativar

---

## Formulário lateral

### Modo de abertura
Painel lateral direito.

### Tamanho sugerido
Largo.

### Modos suportados
- Inclusão
- Edição
- Visualização

---

## Estrutura do formulário

### Seção 1. Identificação

#### Campos
1. **rule_code**
   - Tipo: texto curto
   - Obrigatório: sim
   - Somente leitura na edição: não
   - Exemplo: `R1_IGNORE_DEPLOYMENT_EVENTS`

2. **rule_name**
   - Tipo: texto
   - Obrigatório: sim
   - Exemplo: `Ignorar eventos de deployment`

3. **rule_description**
   - Tipo: textarea
   - Obrigatório: não
   - Finalidade: detalhar a intenção funcional da regra

---

### Seção 2. Escopo do evento

#### Campos
4. **event_origin**
   - Tipo: combo
   - Obrigatório: não
   - Exemplos:
     - Datadog
     - Zabbix
     - SolarWinds
     - GoogleChat
   - Comportamento:
     - vazio = qualquer origem

5. **event_category**
   - Tipo: combo
   - Obrigatório: não
   - Comportamento:
     - vazio = qualquer categoria

6. **event_subcategory**
   - Tipo: combo
   - Obrigatório: não
   - Comportamento:
     - vazio = qualquer subcategoria

7. **environment**
   - Tipo: combo
   - Obrigatório: não
   - Exemplos:
     - prod
     - hml
     - dev
     - unknown
   - Comportamento:
     - vazio = qualquer ambiente

8. **service_name**
   - Tipo: combo com busca
   - Obrigatório: não
   - Comportamento:
     - vazio = qualquer serviço

---

### Seção 3. Critérios de severidade e volume

#### Campos
9. **severity_min**
   - Tipo: número inteiro
   - Obrigatório: não

10. **severity_max**
   - Tipo: número inteiro
   - Obrigatório: não

11. **event_count_min**
   - Tipo: número inteiro
   - Obrigatório: não
   - Finalidade:
     - quantidade mínima de eventos no intervalo para a regra ser válida

12. **event_count_window_minutes**
   - Tipo: número inteiro
   - Obrigatório: não
   - Finalidade:
     - janela de observação para contagem histórica

### Comportamentos
- Se `severity_min` for informado, `severity_max` também deve ser validado.
- `severity_max` não pode ser menor que `severity_min`.
- `event_count_min` não pode ser menor que 1 quando informado.
- `event_count_window_minutes` não pode ser menor que 1 quando informado.

---

### Seção 4. Critérios textuais

#### Campos
13. **text_contains**
   - Tipo: textarea
   - Obrigatório: não
   - Finalidade:
     - texto ou expressão esperada no conteúdo do evento

14. **text_not_contains**
   - Tipo: textarea
   - Obrigatório: não
   - Finalidade:
     - texto ou expressão que impede o casamento da regra

### Observação funcional
- O motor poderá usar comparação textual simples inicialmente.
- Em versões futuras, poderá suportar expressões mais sofisticadas.

---

### Seção 5. Resultado da decisão

#### Campos
15. **decision_status**
   - Tipo: combo
   - Obrigatório: sim
   - Exemplos sugeridos:
     - ignored
     - monitored
     - alert
     - incident_candidate
     - incident_opened
     - closed
   - O domínio final deverá ser alinhado ao motor de decisão.

16. **open_incident**
   - Tipo: switch
   - Obrigatório: sim
   - Padrão: Não

17. **close_event**
   - Tipo: switch
   - Obrigatório: sim
   - Padrão: Não

18. **priority_order**
   - Tipo: número inteiro
   - Obrigatório: sim
   - Finalidade:
     - ordenar a execução das regras
   - Regra:
     - menor número = maior prioridade

---

### Seção 6. Situação

#### Campos
19. **is_active**
   - Tipo: switch
   - Obrigatório: sim
   - Padrão: Sim

20. **valid_from**
   - Tipo: data/hora
   - Obrigatório: não

21. **valid_to**
   - Tipo: data/hora
   - Obrigatório: não

22. **notes**
   - Tipo: textarea
   - Obrigatório: não

---

### Seção 7. Auditoria

#### Campos somente leitura
23. **created_at**
24. **created_by**
25. **updated_at**
26. **updated_by**

---

## Rodapé do formulário

### Botões
- **Salvar**
- **Salvar e continuar**
- **Cancelar**

### No modo visualização
- **Fechar**

---

## Validações obrigatórias

### Obrigatórias
- rule_code
- rule_name
- decision_status
- priority_order
- is_active
- open_incident
- close_event

### Regras de consistência
1. `rule_code` não pode duplicar.
2. `rule_name` não pode ficar vazio.
3. `priority_order` deve ser maior que zero.
4. `severity_max` não pode ser menor que `severity_min`.
5. `valid_to` não pode ser menor que `valid_from`.
6. Pelo menos um critério de escopo ou condição deve ser informado para evitar regra ampla demais, exceto em regras explicitamente globais autorizadas.
7. Não permitir duas regras ativas com mesma prioridade e mesmo escopo exato sem alerta ao usuário.

---

## Regras de negócio importantes

1. A regra só participa da avaliação se `is_active = true`.
2. A execução deve respeitar `priority_order`.
3. A primeira regra aderente deve prevalecer.
4. Regras muito genéricas devem gerar alerta visual no cadastro.
5. Regras que abrem incidente devem ser tratadas com destaque visual.
6. Regras que fecham evento também devem ser destacadas, pois possuem impacto operacional.
7. Campos vazios de escopo significam comportamento curinga, ou seja, aceitam qualquer valor naquele atributo.

---

## Mensagens padrão

### Sucesso
- Regra cadastrada com sucesso.
- Regra atualizada com sucesso.
- Regra inativada com sucesso.
- Regra ativada com sucesso.

### Erro
- Não foi possível salvar a regra.
- Já existe uma regra com este código.
- A prioridade informada é inválida.
- A faixa de severidade está inconsistente.
- A vigência informada está inválida.

### Confirmação
- Deseja realmente salvar esta regra?
- Deseja realmente inativar esta regra?
- Esta regra pode impactar a abertura automática de incidentes. Deseja continuar?

---

## Comportamentos visuais desejados

### Destaques
- Regras que abrem incidente:
  - exibir badge ou ícone de atenção
- Regras inativas:
  - exibir com tom neutro
- Regras de alta prioridade:
  - destacar prioridade muito baixa numericamente, ex.: 1, 2, 3

### Ajuda contextual
Cada campo crítico deve possuir tooltip explicando a finalidade, principalmente:
- event_count_min
- event_count_window_minutes
- decision_status
- open_incident
- close_event
- priority_order

---

## Paginação do grid
- padrão inicial: 10, 25, 50
- ordenação por clique no cabeçalho
- paginação no rodapé
- totalizador de registros

---

## API esperada para integração

### Endpoints sugeridos
- `GET /api/decision-rules`
- `GET /api/decision-rules/{id}`
- `POST /api/decision-rules`
- `PUT /api/decision-rules/{id}`
- `PATCH /api/decision-rules/{id}/activate`
- `PATCH /api/decision-rules/{id}/inactivate`

---

## Modelo de payload sugerido

```json
{
  "rule_code": "R4_INCIDENT_CANDIDATE_APPLICATION_ERROR_BURST",
  "rule_name": "Abrir candidato a incidente para burst de erro de aplicação",
  "rule_description": "Regra para eventos recorrentes de erro de aplicação em janela curta",
  "event_origin": "Datadog",
  "event_category": "application",
  "event_subcategory": "error",
  "environment": "prod",
  "service_name": "api-beneficiario",
  "severity_min": 2,
  "severity_max": 4,
  "event_count_min": 5,
  "event_count_window_minutes": 15,
  "text_contains": "error",
  "text_not_contains": "deployment",
  "decision_status": "incident_candidate",
  "open_incident": true,
  "close_event": false,
  "priority_order": 4,
  "is_active": true,
  "valid_from": null,
  "valid_to": null,
  "notes": "Aplicável para produção"
}
```

---

## Componentes esperados no frontend
- PageHeader
- FilterCard
- DataGrid
- BadgeStatus
- ToggleSwitch
- SideFormDrawer
- FormSection
- ConfirmDialog
- NotificationToast
- TooltipHelper

---

## Observações para geração automática pelo Antigravity
1. Gerar tela padrão com filtro, grid e drawer lateral.
2. Usar combos para domínios fechados.
3. Usar switch para booleanos.
4. Bloquear exclusão física por padrão.
5. Implementar ativação/inativação como ação principal de status.
6. Ordenar grid inicialmente por `priority_order`.
7. Destacar visualmente regras com `open_incident = true`.
8. Manter auditoria em modo somente leitura.
9. Permitir reaproveitamento do padrão para outras entidades do Predict.

---

## Resultado esperado
Ao final, a tela deverá permitir ao usuário operacional:
- localizar regras rapidamente
- entender o escopo de cada regra
- cadastrar novas regras com segurança
- alterar prioridades
- ativar e inativar regras
- reduzir risco de configuração inconsistente no motor de decisão
