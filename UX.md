# Predict - Padrão UX de Formulário CRUD

## Objetivo
Definir um padrão único de experiência de uso para todos os formulários CRUD do Predict, garantindo consistência visual, navegação simples, baixo atrito operacional e fácil geração automatizada pelo Antigravity.

---

## Princípios do padrão
- Interface limpa, corporativa e objetiva.
- Prioridade para produtividade operacional.
- Mesmo comportamento visual em todos os CRUDs.
- Separação clara entre:
  - filtros de pesquisa
  - listagem
  - cadastro/edição
  - ações
- Redução de cliques desnecessários.
- Responsividade mínima para desktop widescreen e notebook.

---

## Estrutura visual da tela

A tela deverá ser dividida em 4 áreas principais:

### 1. Cabeçalho da página
Deve conter:
- título principal da funcionalidade
- subtítulo explicando o objetivo da tela
- breadcrumb opcional
- botão principal de inclusão, quando aplicável

### 2. Área de filtros
Bloco superior recolhível, contendo:
- campos de filtro
- botão **Pesquisar**
- botão **Limpar**
- botão **Filtros avançados** quando necessário

### 3. Área de listagem
Grid central com:
- paginação
- ordenação por coluna
- seleção de linha
- ações por linha
- totalizador de registros
- mensagem amigável quando não houver resultados

### 4. Área de formulário
Exibida em:
- modal lateral (drawer) preferencialmente, ou
- modal central para cadastros simples

O formulário deve servir para:
- inclusão
- edição
- visualização
- exclusão lógica, quando existir

---

## Layout padrão

### Cabeçalho
- Título em destaque
- Subtítulo menor e discreto
- Botão primário no canto direito:
  - `Novo`
  - `Nova Regra`
  - `Novo Parâmetro`
  - conforme a entidade

### Card de filtros
- Fundo claro
- Bordas suaves
- Ícones discretos
- Campos organizados em grade
- Botões alinhados à direita

### Grid de dados
Cada linha deverá permitir:
- visualizar
- editar
- ativar/inativar
- excluir, quando aplicável

Colunas de ação no lado direito.

### Formulário
Abrir preferencialmente em painel lateral direito com largura média/grande.
Esse painel deve conter:
- título da operação
- descrição curta
- formulário dividido por seções
- ações fixas no rodapé

---

## Comportamento padrão por modo

### Modo consulta
- usuário visualiza filtros e grid
- não há formulário aberto inicialmente

### Modo inclusão
- clicar em **Novo**
- abrir painel lateral limpo
- foco no primeiro campo editável

### Modo edição
- clicar em **Editar**
- abrir painel lateral preenchido

### Modo visualização
- clicar em **Visualizar**
- abrir painel lateral em modo somente leitura

### Modo exclusão/inativação
- sempre confirmar antes de executar
- usar modal de confirmação
- evitar exclusão física quando a regra de negócio permitir inativação

---

## Padrão das seções do formulário

Todo formulário deverá, sempre que fizer sentido, seguir esta ordem:

### Seção 1. Identificação
Campos principais da entidade:
- código
- nome
- descrição curta
- tipo/categoria

### Seção 2. Classificação
Campos de agrupamento:
- origem
- categoria
- subcategoria
- ambiente
- serviço
- região
- criticidade
- prioridade

### Seção 3. Regras de negócio
Campos funcionais:
- parâmetros de decisão
- thresholds
- flags booleanas
- janelas de tempo
- textos de condição

### Seção 4. Situação
Campos como:
- ativo/inativo
- versão
- data de vigência
- observações

### Seção 5. Auditoria
Somente leitura:
- criado em
- criado por
- alterado em
- alterado por

---

## Tipos visuais de campos

### Campos texto
- largura proporcional ao conteúdo esperado
- placeholder objetivo
- máscara quando necessário

### Combos
- usar quando houver domínio fechado
- permitir busca interna quando a lista for grande

### Switch
Usar para:
- ativo/inativo
- habilitado/desabilitado
- abre incidente
- fecha evento
- auditar sim/não

### Número
- alinhamento à direita
- validação imediata
- máscara apropriada quando aplicável

### Data e hora
- calendário visual
- formato consistente

### Textarea
- para descrições, observações e expressões
- altura suficiente para leitura sem incômodo

---

## Botões padrão

### Na área de filtros
- Pesquisar
- Limpar

### No grid
- Visualizar
- Editar
- Ativar/Inativar
- Excluir

### No formulário
Rodapé fixo com:
- **Salvar** como ação principal
- **Salvar e continuar** quando fizer sentido
- **Cancelar**
- **Fechar** no modo visualização

---

## Validações
- validar campos obrigatórios antes do envio
- exibir erro abaixo do campo
- evitar mensagens genéricas
- destacar o primeiro campo inválido
- não apagar dados já digitados por erro de validação

Exemplos:
- campo obrigatório
- valor mínimo/máximo inválido
- combinação de campos inconsistente
- duplicidade de código/nome
- janela de tempo inválida

---

## Mensagens padrão

### Sucesso
- Registro salvo com sucesso.
- Registro atualizado com sucesso.
- Registro inativado com sucesso.

### Erro
- Não foi possível salvar o registro.
- Existem campos obrigatórios não preenchidos.
- Já existe um registro com esta identificação.

### Confirmação
- Deseja realmente salvar as alterações?
- Deseja realmente inativar este registro?
- Esta ação poderá impactar o processamento. Deseja continuar?

---

## Paginação e grid
A listagem deverá possuir:
- paginação no rodapé
- quantidade de registros por página
- ordenação por clique no cabeçalho
- destaque visual da linha selecionada
- busca rápida opcional acima do grid
- exportação futura preparada, sem necessidade de implementar agora

---

## Padrão visual
- estilo corporativo, limpo e moderno
- bordas suaves
- espaçamento confortável
- contraste adequado
- sem excesso de cores
- cor primária alinhada à identidade do Predict
- ícones discretos e funcionais

---

## Acessibilidade mínima
- navegação por teclado
- foco visível
- labels associados aos campos
- contraste suficiente
- mensagens legíveis

---

## Responsividade
Prioridade para desktop, mas com adaptação mínima:
- notebook
- resoluções intermediárias
- grid com scroll horizontal quando necessário
- formulário lateral ajustável

---

## Regras para geração automática pelo Antigravity
O gerador deverá assumir como padrão:
1. toda tela CRUD possui filtro + grid + formulário
2. o formulário abre em painel lateral
3. ações do grid ficam na última coluna
4. rodapé do formulário é fixo
5. campos de auditoria são somente leitura
6. switch substitui checkbox sempre que possível
7. exclusão deve preferir inativação
8. mensagens devem seguir padrão corporativo e objetivo

---

## Estrutura esperada de componentes
A implementação gerada deverá prever os seguintes blocos:
- PageHeader
- FilterCard
- DataGrid
- ActionColumn
- SideFormDrawer
- FormSection
- FormFooterActions
- ConfirmDialog
- NotificationToast

---

## Observações finais
Este documento define o padrão visual e comportamental base.
A partir dele, deverão ser gerados os próximos MDs específicos por entidade, tais como:
- parâmetros
- regras
- dicionários
- ambientes
- serviços
- regiões
- padrões
- incidentes
- auditoria
- histórico