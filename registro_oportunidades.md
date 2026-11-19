Predict Discovery - CRUD de Cadastro de Oportunidades
Objetivo
Criar uma tela de CRUD para a funcionalidade Cadastro de oportunidades do módulo Predict Discovery, mantendo rigorosamente a identidade visual já existente no produto Predict.
---
Nome da funcionalidade
Cadastro de oportunidades
Módulo
Predict Discovery
Entidade principal
predict_discovery_opportunities
Entidades de apoio
`predict_discovery_processes`
`predict_discovery_tasks`
`predict_discovery_pain_points`
---
Diretriz principal de UX
A tela deve ser separada, porém não pode fugir da identidade visual atual do dashboard do Predict.
Isso significa manter:
Header superior preto, igual ao já existente.
Faixa superior com os cards dos módulos, no mesmo padrão visual atual.
Módulo Predict Discovery destacado.
Fundo geral em cinza claro.
Card principal branco, com cantos arredondados, borda suave e bastante respiro.
Tipografia limpa, moderna e leve.
Ícones finos em vermelho.
Linguagem visual minimalista e executiva, sem aparência de sistema legado.
Nada de modal pesado como tela principal.
A tela deve parecer uma continuação natural do dashboard atual.
Não fazer
Não usar layout genérico de CRUD antigo.
Não mudar cores-base do produto.
Não usar visual escuro no corpo da tela.
Não transformar a experiência em um formulário engessado.
Não quebrar o padrão dos cards e do espaçamento já existente.
---
Estrutura visual da tela
1. Header global
Manter exatamente o mesmo header já existente:
logo / nome Predict
tagline atual
branding Service IT no canto direito
2. Cards dos módulos
Manter a faixa superior com os cards:
Predict Radar
Predict Discovery
Predict Gate
Predict Builder
Predict Approval
Predict Action
O card Predict Discovery deve permanecer destacado visualmente como módulo ativo.
3. Card principal da funcionalidade
Abaixo dos módulos, apresentar um card branco grande, com o mesmo padrão do dashboard atual.
Cabeçalho interno do card
À esquerda:
ícone vermelho do módulo
texto pequeno em caixa alta: MÓDULO DE OPERAÇÃO
título grande: Cadastro de oportunidades
À direita:
botão Nova oportunidade
botão Atualizar
botão Exportar
4. Linha divisória
Inserir uma linha horizontal sutil separando cabeçalho e área operacional.
5. Área de filtros
A área de filtros deve ficar logo abaixo do cabeçalho da funcionalidade, dentro do mesmo card branco.
Filtros sugeridos
Cliente
Processo
Status
Ganho
Esforço
Risco
Busca textual
Botões de ação
Pesquisar
Limpar
6. Grid principal
A listagem das oportunidades deve ficar abaixo dos filtros, dentro do mesmo card.
Colunas sugeridas
Código
Título
Processo
Área operacional
Ganho
Esforço
Risco
Score
Rank
Status
Responsável
Ações
Ações por linha
Visualizar
Editar
Inativar
---
Comportamento de CRUD
Abertura da funcionalidade
Ao clicar em Cadastro de oportunidades no menu do Discovery, abrir esta nova tela completa.
Inclusão
Ao clicar em Nova oportunidade, abrir um painel lateral direito.
Edição
Ao clicar em Editar, abrir o mesmo painel lateral, preenchido com os dados atuais.
Visualização
Ao clicar em Visualizar, abrir o painel lateral em modo somente leitura.
Inativação
Ao clicar em Inativar, exibir modal de confirmação simples.
Exclusão física
Não deverá existir exclusão física pela interface.  
Utilizar inativação lógica via `is_active`.
---
Painel lateral de formulário
O formulário deverá abrir em um drawer lateral direito, elegante, leve e coerente com o visual atual.
Estrutura do painel
Organizar os campos em blocos visuais:
Bloco 1 - Identificação
Cliente (`client_code`)
Código da oportunidade (`opportunity_code`)
Processo (`process_id`)
Título (`title`)
Categoria da oportunidade (`opportunity_category`)
Área operacional (`operational_area`)
Bloco 2 - Contexto
Resumo (`summary`)
Solicitado por (`requested_by`)
Responsável (`owner_name`)
Referência de origem (`source_reference`)
Tipo de origem (`origin_type`)
Referência de padrão Radar (`radar_pattern_reference`)
Bloco 3 - Classificação
Classificação de ganho (`expected_gain_classification`)
Classificação de esforço (`implementation_effort_classification`)
Classificação de risco (`operational_risk_classification`)
Score de prioridade (`priority_score`)
Rank de prioridade (`priority_rank`)
Bloco 4 - Volumetria
Volume estimado (`estimated_volume`)
Unidade de volume (`volume_unit`)
Valor de frequência (`frequency_value`)
Unidade de frequência (`frequency_unit`)
Tempo economizado em minutos (`estimated_time_saved_minutes`)
Custo evitado (`estimated_cost_avoidance`)
Potencial de automação (%) (`automation_potential_percent`)
Bloco 5 - Controle
Status (`status`)
Motivo de descarte (`discard_reason`)
Referência Builder (`builder_reference`)
Convertido para Builder (`converted_to_builder`)
Ativo/Inativo (`is_active`)
Rodapé do painel
Botões:
Salvar
Salvar e continuar
Cancelar
---
Regras de negócio da tela
1. Processo
O campo Processo deve ser carregado a partir de `predict_discovery_processes`.
2. Código da oportunidade
O campo `opportunity_code` deve ser obrigatório e único por cliente.
3. Título
O título da oportunidade é obrigatório.
4. Classificações
As classificações de ganho, esforço e risco devem ser apresentadas em listas controladas.
5. Rank e score
`priority_score` pode ser informado/calculado.
`priority_rank` deve refletir ordenação de priorização.
6. Descarte
Quando o status indicar descarte/inativação, o campo `discard_reason` deve ser exigido.
7. Conversão para Builder
O campo `converted_to_builder` é informativo e não deve ser livremente alterado sem regra de negócio.
---
Abas ou seções complementares
Após salvar a oportunidade principal, habilitar áreas filhas dentro da própria tela ou em abas internas.
1. Tarefas
Base: `predict_discovery_tasks`
Campos:
Código
Nome da tarefa
Descrição
Sequência
Volume estimado
Unidade
Frequência
Unidade de frequência
Tempo médio
Candidata à automação
Status
2. Dores
Base: `predict_discovery_pain_points`
Campos:
Código
Título da dor
Descrição
Tipo de impacto
Nível de impacto
Evidência
Observações de recorrência
Status

---
Layout funcional sugerido
```text
HEADER GLOBAL PRETO
CARDS DOS MÓDULOS
CARD PRINCIPAL BRANCO

[MÓDULO DE OPERAÇÃO]
Cadastro de oportunidades                          [Nova oportunidade] [Atualizar] [Exportar]

[Filtros...]
[Cliente] [Processo] [Status] [Ganho] [Esforço] [Risco] [Busca] [Pesquisar] [Limpar]

[Grid de oportunidades]
| Código | Título | Processo | Ganho | Esforço | Risco | Score | Rank | Status | Responsável | Ações |

[Drawer lateral direito para inclusão/edição/visualização]
- Identificação
- Contexto
- Classificação
- Volumetria
- Controle
```
---
Comportamento visual esperado
Hover sutil nos botões e linhas da grid
Bordas leves
Sombras discretas
Muito espaço em branco
Ícones vermelhos finos
Botões elegantes e discretos
Drawer lateral suave e moderno
Grid com aparência executiva, não legado
---
Responsividade
Desktop
Prioridade máxima.  
A tela deve ser pensada primeiro para desktop.
Tablet
O drawer lateral pode ocupar mais largura.
Mobile
Não é prioridade nesta etapa, mas a estrutura não deve quebrar totalmente.
---
Resultado esperado
Entregar uma tela de CRUD que:
respeite totalmente a identidade visual atual do Predict;
pareça continuação natural do dashboard;
permita cadastrar, consultar, editar, visualizar e inativar oportunidades;
sirva como padrão para as demais telas do módulo Predict Discovery.
---
Observação final para implementação
O objetivo não é criar apenas um CRUD funcional.  
O objetivo é criar uma tela de operação com padrão visual premium, coerente com o restante do produto Predict.