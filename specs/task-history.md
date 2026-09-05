# SPEC-TASK-HISTORY: Histórico, Comentários e Linha do Tempo de Colunas

**Status:** approved

**Revisão funcional:** aprovada explicitamente por PO em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Separar conversas humanas, auditoria geral e movimentações entre colunas no detalhe da tarefa. O detalhe mantém
seis abas: `Descrição`, `Comentários`, `Subtarefas`, `Anexos`, `Histórico` e `Linha do tempo`. A linha do tempo
substitui o atual `Grafo de estados` por uma leitura simples, textual e cronológica.

## As seis abas

1. **Descrição:** conteúdo e dados editáveis da tarefa.
2. **Comentários:** conversas humanas internas, fora da auditoria automática.
3. **Subtarefas:** hierarquia e ações de subtarefas.
4. **Anexos:** arquivos vinculados à tarefa.
5. **Histórico:** auditoria geral automática das alterações da tarefa.
6. **Linha do tempo:** somente criação na coluna inicial e movimentações posteriores entre colunas.

O modal centralizado, responsivo, acessível e com preservação do contexto de origem continua regido pela D49.

## Separação entre comentários, histórico e linha do tempo

### Comentários

- Contêm exclusivamente mensagens escritas por pessoas.
- Não entram no Histórico apenas por terem sido publicados, editados ou removidos.
- Não entram na Linha do tempo.
- Permanecem em armazenamento, contrato e permissão próprios.

### Histórico

- Contém eventos automáticos de auditoria sobre criação e alterações da tarefa.
- Pode registrar campo alterado, valor anterior, valor novo, ator e data/hora.
- Inclui mudanças relevantes que não sejam movimentação entre colunas, como responsável, prioridade, título e datas.
- Movimentações continuam preservadas internamente para auditoria, mas não aparecem na lista visual do Histórico;
  sua visualização dedicada ocorre somente na Linha do tempo.
- Não contém comentários.

### Linha do tempo

- Mostra somente o caminho cronológico real da tarefa entre colunas.
- Não é grafo, diagrama, mapa, React Flow ou visualização de nós/arestas.
- Não agrega duração, número de visitas, lead time ou tempo médio por coluna.
- Não exibe alterações gerais de campo nem comentários.
- É ordenada da movimentação **mais recente para a mais antiga**.

## Conteúdo da Linha do tempo

### Evento inicial

O primeiro registro cronológico representa a criação da tarefa na coluna inicial com o texto:

`Tarefa criada na coluna {Nome da coluna}`

Também apresenta data/hora e pessoa responsável. Novos registros usam obrigatoriamente o nome completo preservado
no evento. Quando houver nome, o e-mail fica oculto. Registro legado sem nome pode usar o e-mail como fallback;
sem nome nem e-mail, usa rótulo neutro como `Pessoa não registrada`, sem inventar autoria.

### Movimentações posteriores

Cada item apresenta obrigatoriamente:

- data e hora da movimentação;
- nome completo da pessoa que moveu;
- coluna anterior;
- nova coluna.

Forma textual recomendada:

`{Pessoa} moveu de {Coluna anterior} para {Nova coluna}`

A data/hora aparece como metadado legível no mesmo item. E-mail fica oculto quando houver nome e só pode aparecer
como fallback de dados legados sem nome, conforme D63.

## Regras funcionais

1. A fonte da Linha do tempo são os registros persistidos de entrada/saída entre colunas, não eventos inferidos da
   posição atual.
2. Uma movimentação confirmada gera exatamente um item cronológico correspondente.
3. Movimento recusado ou revertido não gera item.
4. A criação gera o evento inicial na coluna em que a tarefa foi efetivamente persistida.
5. A ordenação é decrescente por data/hora; empates usam identificador/ordem persistida determinística.
6. Transferir a tarefa entre quadros também registra a mudança da coluna anterior para a nova coluna, preservando
   o mesmo histórico da tarefa.
7. Renomear uma coluna não reescreve silenciosamente o passado; os nomes apresentados devem usar snapshot histórico
   quando disponível. Até existir snapshot, o fallback para o nome atual deve ser identificado como gap.
8. `ActorName` guarda o nome completo como snapshot da autoria no momento da movimentação e não muda depois.
9. Novos eventos não podem ser gravados sem nome completo do ator autenticado.
10. Ausência posterior da conta Identity não invalida o registro; a leitura prioriza `ActorName`.
11. Para legado sem `ActorName`, o e-mail pode ser fallback; existindo nome, e-mail não aparece.
12. A alteração do `WorkItem`, fechamento/abertura de `StageHistory` e inclusão dos eventos correspondentes são
    atômicos na mesma unidade de trabalho.
13. Comentários permanecem fora do histórico automático conforme D47.
14. A Linha do tempo respeita a mesma permissão de leitura da tarefa e o isolamento do tenant.

## Histórico estruturado

`TaskEvent.Payload` para `field_changed` permanece estruturado em antes/depois:

```json
{
  "changes": [
    {
      "field": "Priority",
      "displayName": "Prioridade",
      "oldValue": "1",
      "oldValueText": "Baixa",
      "newValue": "3",
      "newValueText": "Alta"
    }
  ]
}
```

O payload permanece `nvarchar(max)`. Eventos legados incompatíveis devem possuir fallback de leitura seguro sem
reescrever ou apagar o conteúdo original.

## Estado atual comprovado no código

- `TaskDetailDrawer.tsx` já separa `Comentários` e `Histórico`.
- O detalhe possui seis abas, mas a sexta ainda se chama `Grafo de estados`.
- `TaskStateGraph.tsx` usa React Flow para renderizar nós, arestas, visitas e permanência acumulada.
- `GET /api/WorkItems/{id}/state-graph` devolve `TaskStateGraphDto` com nodes/edges.
- `StageHistory` já possui `StageId`, `EnteredAt`, `LeftAt`, `ActorId`, `ActorName` e `Reason`.
- o handler atual deriva arestas com data/hora e ator a partir de `StageHistory`.
- `TaskEvent` e o feed de histórico permanecem separados de `Comment`.
- o modelo atual não preserva comprovadamente snapshots dos nomes da coluna anterior/nova para todas as movimentações.

## Gaps entre contrato e implementação

1. **Aba incorreta:** a sexta aba ainda é `Grafo de estados`, não `Linha do tempo`.
2. **Visualização incorreta:** React Flow, nós, arestas, visitas e permanência contradizem a lista simples aprovada.
3. **Contrato inadequado:** o endpoint retorna grafo agregado, não itens cronológicos prontos para leitura.
4. **Evento inicial:** não há apresentação comprovada de `Tarefa criada na coluna X`.
5. **Ordem:** o grafo não garante lista decrescente da movimentação mais recente para a mais antiga.
6. **Conteúdo:** a UI atual não apresenta, em cada item, coluna anterior e nova como texto simples.
7. **Snapshot da coluna:** renomear coluna pode alterar a interpretação visual do passado porque `StageHistory`
   referencia `StageId`, sem nomes anterior/novo comprovadamente preservados.
8. **Identidade:** `ActorName` continua opcional no modelo; novos eventos ainda precisam garantir nome completo,
   ocultar e-mail quando houver nome e restringir fallback de e-mail ao legado.
9. **Duplicação visual:** movimentações podem aparecer no Histórico geral e na Linha do tempo sem distinção clara.
10. **Cobertura:** faltam testes da ordem decrescente, evento inicial, conteúdo de cada item, ausência do grafo e
    separação de comentários.

## API desejada

Pode evoluir o endpoint existente ou criar contrato equivalente sem duplicar fontes de verdade. A resposta funcional
da Linha do tempo deve fornecer itens ordenáveis contendo:

- `id`;
- `kind`: `created_in_column` ou `column_moved`;
- `occurredAt`;
- `actorId` e `actorName` opcionais;
- `fromColumnId` e `fromColumnName` opcionais no evento inicial;
- `toColumnId` e `toColumnName` obrigatórios.

O servidor valida acesso à tarefa e tenant. A UI não reconstrói a movimentação combinando respostas ambíguas.

## Impacto de dados

- Nenhuma migration está autorizada nesta revisão documental.
- `StageHistory`, `TaskEvent` e seus dados atuais são preservados.
- Se a implementação precisar persistir snapshot dos nomes das colunas ou distinguir explicitamente o evento
  inicial, deve apresentar auditoria de dados e solicitar `G-MIGRATION`.
- Mudança da estrutura imutável ou semântica histórica exige `G-HISTORY` antes da implementação.

## Critérios de aceite

- **Dado** o detalhe de uma tarefa, **quando** o modal abre, **então** apresenta as seis abas aprovadas e não exibe
  `Grafo de estados`.
- **Dado** uma tarefa recém-criada na coluna `Backlog`, **quando** a Linha do tempo é aberta, **então** contém
  `Tarefa criada na coluna Backlog` com data/hora e autoria disponível.
- **Dado** movimentos `Backlog → Em andamento → Testes`, **quando** a Linha do tempo é aberta, **então** `Em andamento
  → Testes` aparece antes de `Backlog → Em andamento` e o evento de criação aparece por último.
- **Dado** uma movimentação, **quando** seu item é exibido, **então** mostra data/hora, pessoa, coluna anterior e nova.
- **Dado** uma alteração de prioridade, **quando** o Histórico é aberto, **então** mostra antes/depois e não cria item
  na Linha do tempo.
- **Dado** um comentário publicado, **quando** Comentários, Histórico e Linha do tempo são consultados, **então** ele
  aparece somente em Comentários.
- **Dado** uma movimentação revertida por falha, **quando** as duas abas históricas são recarregadas, **então** não
  existe registro parcial.
- **Dado** autoria removida do Identity, **quando** a Linha do tempo é consultada, **então** o snapshot preservado é
  exibido; no legado sem nome, usa e-mail preservado quando existir ou rótulo neutro na ausência de ambos.
- **Dado** nome completo disponível, **quando** Histórico ou Linha do tempo exibem autoria, **então** mostram o nome
  e não mostram o e-mail.
- **Dado** um registro legado sem nome, **quando** houver e-mail preservado, **então** ele pode ser usado como fallback.
- **Dado** viewport móvel, **quando** a Linha do tempo é aberta, **então** todos os itens são legíveis sem canvas,
  pan ou zoom.

## Testes e homologação necessários

- Domínio/aplicação: atomicidade da movimentação e `StageHistory`.
- API: evento inicial, movimentos, ordem decrescente, autorização e tenant.
- React: seis abas, ausência do grafo, lista textual, estados vazio/erro/carregando e autoria neutra.
- Playwright: criação e movimentos em banco E2E dedicado, conferindo ordem e conteúdo da Linha do tempo.
- Separação: comentário apenas em Comentários; mudança geral apenas no Histórico; movimento apenas na Linha do tempo.
- Homologação manual por PO antes de promoção.

## Human Gates

- `G-SPEC`: aprovado explicitamente por PO para este contrato.
- `G-SCOPE`: decisão registrada na D61, sucedendo a parte visual da D44/D49.
- `G-HISTORY`: obrigatório se estrutura ou semântica imutável de `StageHistory`/`TaskEvent` mudar.
- `G-MIGRATION`: obrigatório se novos snapshots ou marcadores forem persistidos.
- `G-DEPLOY`: obrigatório antes de promoção institucional.

## Referências

- `AGENTS.md`.
- `DECISIONS.md` — D44, D46, D47, D49, D61 e D63.
- `specs/audit-leadtime-history.md`.
- `specs/work-item-management.md`.
- `src/Detran.Kanban.Domain/Entities/StageHistory.cs`.
- `src/Detran.Kanban.Domain/Entities/TaskEvent.cs`.
- `src/Detran.Kanban.Application/Features/TaskFeed/Queries/GetTaskFeedQueries.cs`.
- `src/Detran.Kanban.Application/Features/TaskFeed/Dtos/TaskFeedDtos.cs`.
- `src/Detran.Kanban.Api/Controllers/TaskFeedController.cs`.
- `src/Detran.Kanban.Web/src/components/TaskDetailDrawer.tsx`.
- `src/Detran.Kanban.Web/src/features/task/TaskStateGraph.tsx`.

## Rastreabilidade

Decisão humana de PO -> D61 -> `SPEC-TASK-HISTORY` (`approved`) -> gap documental -> futura implementação com
gates necessários -> testes -> homologação manual.
