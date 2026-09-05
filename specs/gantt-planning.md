# SPEC-GANTT-001: Planejamento temporal, Gantt e calendário

**Status:** approved
**Baseline:** as-built com decisão funcional posterior de ocultação
**Decisão humana:** PO, 2026-08-24

## Objetivo

Documentar o que já existe internamente para Gantt e calendário e estabelecer o contrato visível atual: **todas as entradas e visualizações de Gantt e planejamento temporal ficam ocultas na interface por enquanto**.

Essa decisão não exige excluir componentes, campos ou dados existentes. Ela apenas retira essas superfícies da experiência do usuário até nova decisão explícita.

## Escopo

- Visão Gantt do quadro (`BoardGantt`).
- Calendário temporal do quadro (`BoardCalendar`).
- Abas, botões, menus, atalhos, links ou rotas que deem acesso a essas visões.
- Campos temporais já existentes em tarefas, projetos e sprints.

Neste documento, “planejamento temporal” significa exclusivamente Gantt, calendário e cronograma associados a datas. Product Backlog, planejamento de sprint e organização comum do Kanban não são ocultados por esta spec.

## Estado atual comprovado (as-built)

1. O componente `BoardGantt.tsx` existe e renderiza barras por tarefa usando `createdAt` como início e `dueDate` como fim; sem vencimento, usa duração visual padrão de três dias.
2. O Gantt agrupa tarefas por etapa, destaca atrasos, mostra o dia atual, permite abrir a tarefa e usa a cor do tipo quando disponível.
3. O componente `BoardCalendar.tsx` existe e renderiza uma grade mensal, posicionando tarefas por `dueDate`, destacando atrasos e limitando visualmente a quatro itens por dia antes do indicador “+N mais”.
4. Os dois componentes derivam seus dados dos `WorkItems` já carregados; não existe endpoint dedicado de Gantt ou calendário.
5. O domínio já possui campos temporais em `WorkItem`, `Project` e `Sprint`. Esses campos atendem também a outras funcionalidades e não devem ser removidos.
6. `Kanban.tsx` ainda importa `BoardGantt` e `BoardCalendar`, oferece “Calendário” e “Gantt” no seletor de visão e renderiza os componentes quando essas opções são escolhidas.

## Contrato funcional vigente

1. A interface não deve exibir opção de Gantt, calendário ou cronograma temporal.
2. Não deve existir acesso visível por aba, botão, menu, link, atalho, card de dashboard ou chamada equivalente.
3. Uma URL direta não deve tornar essas visões acessíveis enquanto estiverem ocultas.
4. A tela principal do trabalho continua sendo o Kanban; ocultar este módulo não altera tarefas, quadros, colunas, backlog ou sprints.
5. Componentes e lógica interna podem permanecer no código como implementação dormente, desde que não sejam carregados nem oferecidos ao usuário.
6. Campos como `StartDate`, `DueDate` e `CompletedAt` permanecem disponíveis para as demais regras do produto.
7. Nenhuma funcionalidade avançada de planejamento passa a fazer parte do contrato por existir código interno relacionado.

## Fora do escopo

- Excluir os componentes `BoardGantt` e `BoardCalendar`.
- Remover ou migrar campos de data do banco.
- Ocultar Product Backlog ou planejamento de sprint.
- Implementar dependências visuais, marcos, caminho crítico, baseline, capacidade, zoom temporal, drag-and-drop ou redimensionamento de barras.
- Criar endpoints, exportações ou integrações externas de calendário.

## Gap entre a spec e o sistema

### G-GANTT-001 — entradas temporais ainda visíveis

`Kanban.tsx` ainda inclui as opções `Calendário` e `Gantt` no seletor de visualização e renderiza `BoardCalendar` e `BoardGantt`. Isso diverge do contrato aprovado de ocultação.

**Correção esperada em tarefa futura:** retirar essas opções e impedir acesso direto às visões, preservando os componentes e dados internos. Esta atualização de spec é documental e não autoriza alteração de código de produto neste momento.

## Critérios de aceitação

- **Dado** um usuário autorizado no Kanban, **quando** ele inspeciona todas as opções de visualização, **então** não encontra Gantt, Calendário ou Planejamento temporal.
- **Dado** o menu desktop ou mobile, **quando** o usuário percorre navegação, ações e atalhos, **então** não encontra entrada para essas visões.
- **Dado** que os campos temporais já possuem dados, **quando** as visões são ocultadas, **então** nenhum dado é removido ou alterado.
- **Dado** o Product Backlog e o planejamento de sprint, **quando** esta regra é aplicada, **então** essas funcionalidades continuam disponíveis.
- **Dado** o código interno de Gantt e calendário, **quando** a aplicação é usada normalmente, **então** esses componentes não são renderizados nem acessíveis.

## Homologação manual

Estado atual: **pendente; há gap visual conhecido**.

Checklist após a futura correção:

1. Abrir o Kanban em desktop e confirmar que não existem opções “Gantt” e “Calendário”.
2. Repetir a validação em viewport mobile e no menu sobreposto.
3. Verificar menus, atalhos, breadcrumbs, dashboards e ações contextuais.
4. Tentar acesso por histórico do navegador ou URL anteriormente conhecida e confirmar que a visão não abre.
5. Confirmar que Kanban, Product Backlog e planejamento de sprint continuam funcionando.
6. Confirmar que datas existentes de tarefas, projetos e sprints foram preservadas.

## Condições para reativação futura

Gantt, calendário ou outra visão temporal somente podem voltar à interface depois de:

1. nova decisão explícita do PO sobre qual visão será reativada;
2. atualização e aprovação desta spec por `G-SPEC`;
3. definição de permissões, navegação, filtros, comportamento mobile e limites de volume;
4. decisão específica sobre `createdAt` versus `StartDate` como início real da tarefa;
5. definição separada para qualquer recurso avançado, sem inferi-lo do código dormente;
6. testes unitários e E2E cobrindo visibilidade, autorização, navegação e comportamento da visão reativada.

## Impacto em dados e contratos

- Nenhuma migration é necessária para ocultar a interface.
- Nenhum endpoint novo é criado.
- Os contratos atuais de tarefa, projeto e sprint permanecem inalterados.
- Dados temporais importados ou cadastrados continuam preservados.

## Autorização

Enquanto o módulo estiver oculto, nenhuma função de usuário deve acessá-lo pela interface. Uma reativação futura precisará definir autorização explícita antes da implementação.

## Mapeamento de testes

- Teste de componente/página: o seletor do Kanban não apresenta Gantt nem Calendário.
- Teste de navegação: menus e ações globais/contextuais não apresentam entradas temporais.
- Teste E2E desktop e mobile: as visões permanecem inacessíveis e o Kanban continua operacional.
- Teste de regressão: ocultar as visões não altera datas nem os fluxos de backlog e sprint.

## Riscos

- Ocultar somente o botão e manter outro caminho de acesso deixaria o contrato incompleto.
- Remover campos de data ou componentes sem necessidade poderia causar perda de dados ou retrabalho futuro.
- Tratar recursos avançados já mencionados como aprovados ampliaria indevidamente o escopo.

## Rastreabilidade

Decisão humana de 2026-08-24 → `SPEC-GANTT-001` → gap `G-GANTT-001` → futura tarefa de ocultação → testes → homologação manual.
