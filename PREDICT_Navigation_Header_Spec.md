# Predict — Especificação de Navegação e Identidade Visual Global

**Para:** Antigravity (Implementação de Interface)
**De:** Service IT — Produto Predict
**Data:** Abril / 2026
**Versão:** 1.0

\---

## Decisão de produto

Após avaliação da UX implementada pelo Antigravity nas telas de detalhe do Radar, foi tomada a seguinte decisão de arquitetura de navegação:

> \*\*O header preto é o único elemento fixo e universal do Predict. Tudo abaixo dele é livre para ter personalidade própria por módulo.\*\*

Esta decisão substitui qualquer padrão anterior de navegação global e deve ser aplicada imediatamente em todas as telas existentes e futuras do produto.

\---

## O Header — Elemento Imutável

### Visual

```
┌─────────────────────────────────────────────────────────────────────┐
│ ⚙ PREDICT  — Especialistas mais rápidos. Operações mais inteligentes.          S SERVICEIT  ? │
└─────────────────────────────────────────────────────────────────────┘
```

* **Fundo:** preto `#0D0D0D` ou equivalente atual
* **Linha inferior:** vermelha `#E74C3C`, 2px — marca registrada do produto
* **Logo esquerdo:** ícone de engrenagem + "PREDICT" em branco bold + tagline em cinza claro
* **Logo direito:** "S SERVICEIT" em branco + ícone de ajuda "?"
* **Altura:** fixa, igual em todas as telas

### Comportamento

* **Sempre presente:** aparece em 100% das telas do produto sem exceção
* **Sempre no topo:** posição fixed no eixo Y, z-index acima de qualquer outro elemento
* **Nunca muda:** cor, tipografia, altura e conteúdo são imutáveis independente do módulo ou contexto
* **Não é clicável como navegação:** o header não serve como menu — ele é identidade, não navegação
* **Responsivo:** em mobile, a tagline pode ser ocultada para economizar espaço, mas ícone + "PREDICT" + logos permanecem

### O que NÃO fazer com o header

* ❌ Não adicionar itens de menu ao header
* ❌ Não mudar a cor de fundo por módulo
* ❌ Não adicionar breadcrumbs ou indicadores de localização dentro do header
* ❌ Não remover a linha vermelha inferior em nenhuma tela
* ❌ Não usar versões alternativas do header (compacto, expandido, colorido)

\---

## Tela Inicial (Home) — Único lugar dos 6 cards de módulo

### O que muda

Os 6 cards de módulo (Predict Radar, Predict Discovery, Predict Gate, Predict Builder, Predict Approval, Predict Action) **deixam de ser um elemento de navegação global** presente em todas as telas.

Eles existem **apenas na tela inicial (home)** do Predict, como ponto de entrada para cada módulo.

### Comportamento da home

* Exibe o header fixo no topo
* Abaixo do header: os 6 cards de módulo como grade de navegação
* Cada card leva o usuário para a tela principal daquele módulo
* A home é o único lugar onde todos os 6 módulos são visíveis simultaneamente
* A home pode ser acessada a qualquer momento clicando no logo "PREDICT" no header

### O que NÃO fazer

* ❌ Não repetir os 6 cards em telas internas de módulos
* ❌ Não usar os 6 cards como "breadcrumb" de localização
* ❌ Não manter os cards visíveis em telas de detalhe ou operação

\---

## Telas Internas de Módulos — Liberdade com consistência

### Princípio

Cada módulo é **livre para definir sua própria linguagem visual, navegação interna e layout** desde que o header esteja sempre presente no topo. Não existe um template obrigatório para telas internas.

### O que isso significa na prática

O Antigravity pode e deve criar personalidade visual distinta para cada módulo:

|Módulo|Sugestão de personalidade (não obrigatório)|
|-|-|
|**Predict Radar**|Barra lateral escura com navegação vertical — já implementado e aprovado|
|**Predict Discovery**|Grade de cards com filtros laterais — sensação de "catálogo de oportunidades"|
|**Predict Gate**|Layout de checklist/wizard — sensação de "portão de validação"|
|**Predict Builder**|IDE-like com painel lateral de componentes — sensação de "ambiente de desenvolvimento"|
|**Predict Approval**|Layout de fila com painéis de aprovação — sensação de "sala de decisão"|
|**Predict Action**|Dashboard operacional com painéis em tempo real — sensação de "cockpit de operação"|

Essas sugestões são inspiração, não obrigação. O Antigravity tem liberdade criativa total para as telas internas.

### O que é obrigatório em todas as telas internas

* ✅ Header preto com linha vermelha no topo — sempre
* ✅ Forma de retornar à home (clicar no logo "PREDICT" no header)
* ✅ Indicação clara de em qual módulo o usuário está (título da tela, badge, ou barra lateral)
* ✅ Consistência interna dentro do mesmo módulo — se o Radar usa barra lateral, todas as telas do Radar usam barra lateral

### O que é proibido em telas internas

* ❌ Ocultar ou modificar o header
* ❌ Exibir os 6 cards de módulo como navegação
* ❌ Misturar padrões visuais diferentes dentro do mesmo módulo (ex: barra lateral em algumas telas e tabs em outras do mesmo módulo)

\---

## Hierarquia de navegação

```
HEADER (fixo, imutável, universal)
    └── HOME (os 6 cards de módulo)
            ├── Predict Radar
            │       └── Telas internas do Radar (navegação livre)
            ├── Predict Discovery
            │       └── Telas internas do Discovery (navegação livre)
            ├── Predict Gate
            │       └── Telas internas do Gate (navegação livre)
            ├── Predict Builder
            │       └── Telas internas do Builder (navegação livre)
            ├── Predict Approval
            │       └── Telas internas do Approval (navegação livre)
            └── Predict Action
                    └── Telas internas do Action (navegação livre)
```

\---

## Transição entre módulos

### Como o usuário troca de módulo

1. Clica no logo "PREDICT" no header → volta para a home
2. Na home, clica no card do módulo desejado → entra no módulo

Não existe navegação direta entre módulos sem passar pela home. Isso é intencional — a home é o hub central e o momento em que o usuário tem visão de todos os módulos.

### Comportamento da transição

* A transição home → módulo deve ter animação suave (fade ou slide)
* O header não pisca nem recarrega durante a transição — ele é fixo
* O conteúdo abaixo do header é o único que muda

\---

## Referência visual aprovada

A tela de detalhe do Predict Radar implementada pelo Antigravity — com header preto fixo + barra lateral escura + conteúdo principal à direita — é a **referência de qualidade aprovada** para telas internas. Ela demonstra como o header convive com navegação interna de forma elegante e consistente.

Esta tela serve como benchmark visual para as próximas implementações.

\---

## Impacto nas telas já implementadas

|Tela|Ação necessária|
|-|-|
|Home com 6 cards|✅ Manter como está — é o ponto de entrada correto|
|Telas de módulo com header + 6 cards visíveis simultaneamente|⚠️ Remover os 6 cards das telas internas — manter apenas o header|
|Tela de detalhe do Radar (barra lateral escura)|✅ Aprovada — serve como referência|
|Demais telas internas ainda não implementadas|🆓 Liberdade criativa — seguir o princípio deste documento|

\---

## Critério de aceite

* \[ ] O header preto com linha vermelha aparece em 100% das telas do produto
* \[ ] Os 6 cards de módulo aparecem apenas na tela home
* \[ ] Nenhuma tela interna de módulo exibe os 6 cards como navegação
* \[ ] Clicar no logo "PREDICT" no header retorna o usuário para a home em qualquer tela
* \[ ] A tela de detalhe do Radar mantém o padrão de barra lateral já implementado
* \[ ] Cada módulo mantém consistência visual interna entre suas próprias telas
* \[ ] O header não é modificado em nenhuma tela (cor, altura, conteúdo)

\---

## Resumo em uma frase

> O header preto é a assinatura imutável do Predict. A home é o hub dos módulos. Cada módulo é um universo visual próprio.

\---

*Documento gerado por Service IT — Produto Predict | Abril/2026*

