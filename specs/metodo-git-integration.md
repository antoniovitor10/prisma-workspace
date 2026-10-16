# SPEC-METODO-GIT-INTEGRATION: Método de trabalho e integração Git

**Status:** draft

**Data:** 2026-10-16
**Origem:** tarefa 8 do PO, contrato do rastreador Prisma e decisões de mapeamento fornecidas pelo Maestro.
**Entrega desta revisão:** somente documentação; não aprova implementação, migration, alteração de workflow, histórico ou configuração de repositório.

## Objective

Especificar as fases 2 e 3 da integração do método de trabalho com o Prisma: observar GitHub/GitLab, associar branches e PRs às tasks, aplicar transições autorizadas e publicar a autorização de merge. O Prisma é a fonte de verdade do status; o conteúdo da HU, PRD e task permanece nos documentos do método, inclusive quando esses documentos ficam fora do Git.

## Context

A fase 1 está sendo desenvolvida separadamente pelo Maestro: adaptador fino em TypeScript, em `tools/prisma-mcp`, para status, comentários e vínculo de PR. Esta spec consome esse contrato; não declara a fase 1 implementada nem redefine sua API concreta.

As fontes recebidas são `prisma.md`, `fluxo.md` e `local.md`, na pasta de handoff `~/projetos/prisma-handoff/metodo/`. São referências de planejamento externas ao repositório; os requisitos necessários à revisão estão descritos abaixo, sem depender de acesso a essa pasta na implantação.

Mapeamento já decidido pelo PO:

| Conceito do método | Representação no Prisma |
|---|---|
| Task `T-0001` | Work item de topo do quadro, sem `ParentId`, com valor no campo personalizado de projeto **ID do metodo** (sem acento); ID único dentro do projeto, não global |
| HU `HU-0001` | Valor no campo personalizado de projeto **HU** da task e tag com o mesmo nome, por exemplo **HU-0001**; a HU não vira work item |
| Status | Colunas **Backlog**, **Pronta**, **Em desenvolvimento**, **Em revisao**, **Em teste**, **Concluida**, **Bloqueada** |
| PR na fase 1 | Comentário padronizado e campo personalizado **PR**, gravados por `vincular_pr` |

Os nomes da API do método usam `backlog`, `pronta`, `em-desenvolvimento`, `em-revisao`, `em-teste`, `concluida` e `bloqueada`. A integração resolve cada um para um `StageId` configurado no quadro, mantendo a coluna como status canônico (D65/D89); não cria um segundo status no work item. Não converte automaticamente essas sete colunas em categorias abertas/concluídas: esse mapeamento precisa ser validado antes da implementação.

Atualização do PO em 2026-10-16: as tasks ficam no topo para aparecerem no Kanban, cuja consulta lista itens sem pai (`WorkItemRepository.cs`, filtro por `ParentId`). O vínculo com a HU usa o campo **HU** e a tag, sem hierarquia de work items.

No modo local, status e histórico ficam no frontmatter da task. No modo Prisma, as tasks não têm `status:` e não há escrita de volta nos arquivos. O status próprio da HU permanece em seu documento, conforme o contrato; mover uma task não conclui automaticamente a HU.

## Scope

- Fase 2: recebimento seguro de webhooks GitHub, desenho equivalente para GitLab, vínculo e identificação de tasks, máquina de transições, idempotência, reconciliação e visualização de PRs não vinculados.
- Exigência de autorização na API do Prisma em todos os canais, além da validação do adaptador da fase 1.
- Fase 3: publicação de `prisma/autorizacao`, proteção da principal e observação de deploy bem-sucedido.
- Proposta de persistência, segurança, auditoria, critérios de aceite e testes para implementação futura após os gates aplicáveis.

## Out of Scope

- Implementar código, schema, migrations, workers, workflows de CI, tokens, webhook real ou proteção de branch nesta tarefa.
- Criar branches, abrir PRs, fazer merge ou iniciar deploy pelo Prisma. Essas operações continuam no provedor e nas ferramentas do executor.
- Versionar documentos do método contra a configuração da equipe, exigir ID no título ou ler arquivos locais do desenvolvedor a partir do servidor.
- Substituir a fase 1, importar automaticamente todo o histórico Git ou manter status em arquivos e Prisma ao mesmo tempo.
- Concluir HU automaticamente, mudar regras de descendência/categorias ou aprovar human gates por automação.

## Functional Requirements

### RF-01 — Cadastro e vínculo de repositório

1. Cada integração identifica organização, projeto, quadro do método, provedor, instância, ID estável do repositório, principal configurada e referência externa ao segredo. Renomear repositório não altera sua identidade. Não presumir que a principal se chama `main`.
2. O vínculo exige permissão administrativa no escopo. Uma entrega não pode escolher seu tenant/projeto por um campo arbitrário do payload. Nesta proposta, cada integração tem um projeto destinatário inequívoco; repositórios compartilhados por vários projetos ficam pendentes de desenho próprio.
3. Associar os sete status a IDs de colunas existentes e validar pertencimento ao quadro. Coluna ausente, excluída ou associação ambígua suspende as transições afetadas, sem criar coluna ou adivinhar destino. Configuração de categoria e efeitos na descendência segue D62/D89, com decisão pendente indicada ao final.

### RF-02 — Entrada GitHub autenticada

1. Propor `POST /api/integrations/git/github/{integrationId}/webhook`, via HTTPS, com corpo original preservado até validar a assinatura. O ID identifica a configuração; não é uma credencial.
2. Calcular HMAC-SHA256 dos bytes originais com segredo aleatório exclusivo do repositório; validar `X-Hub-Signature-256`, prefixo `sha256=`, tamanho e hexadecimal, comparando os digests em tempo constante. Não reserializar JSON nem aceitar SHA-1 como substituto. Essa verificação segue a [documentação de assinatura do GitHub](https://docs.github.com/en/webhooks/using-webhooks/validating-webhook-deliveries).
3. O segredo fica em configuração externa/cofre, separado dos tokens Prisma e da credencial de leitura/escrita Git. Não aparece no Git, URL, histórico, logs ou resposta. Rotação pode aceitar a versão anterior por prazo configurado e auditado, nunca indefinidamente.
4. Após autenticar, conferir ID do repositório e instalação autorizada; validar cabeçalhos de evento e entrega. Limitar tamanho do corpo e requisições por integração. Payload válido de outro repositório não pode gerar vínculo nem movimento.
5. Aceitar apenas eventos necessários: `create` de branch, `pull_request` nas ações pertinentes, `pull_request_review` para mudanças solicitadas e, na fase 3, `deployment_status`. `ping` verifica conectividade sem mover tarefas. Eventos autenticados fora do escopo são registrados como ignorados.
6. Persistir a entrada de forma durável antes de responder sucesso e processar fora da requisição. Falha ao persistir não recebe confirmação de aceite. Falhas posteriores são retomadas pela fila persistente e pela reconciliação; não presumir reentrega automática do provedor.

### RF-03 — Idempotência, concorrência e retenção

1. Manter tabela de entregas com unicidade por integração, provedor e `X-GitHub-Delivery`. Reentrega reconhecida retorna sucesso sem reaplicar efeitos. Mesmo ID com conteúdo divergente é conflito, não uma nova operação. Validar assinatura inclusive para IDs já conhecidos. O GitHub preserva o ID em reentregas, conforme suas [boas práticas de webhooks](https://docs.github.com/en/webhooks/using-webhooks/best-practices-for-using-webhooks).
2. Registrar estados recebido, processando, processado, ignorado e falha recuperável/definitiva, tentativas, correlação, hash do corpo e motivo do resultado. Recebido não significa processado; uma falha não pode tornar o evento permanentemente descartado como duplicata.
3. Entrega e reconciliação passam pelo mesmo serviço de domínio e pela mesma trava/versão de concorrência por task. Na transação local, validar origem atual, aplicar movimento e histórico e registrar o efeito consumido. Uma queda antes do commit não deixa movimento parcial; depois do commit, uma retomada não duplica histórico.
4. A deduplicação semântica distingue reentrega de nova mudança legítima: usa repositório, PR, fato/versionamento observado e transição, não somente `task + destino`. Após voltar ao desenvolvimento, um novo ciclo de revisão continua possível. Fatos já consumidos não voltam a produzir movimento.
5. Eventos atrasados devem ser comparados ao estado/versionamento atual do PR e da task. Não ordenar somente pela chegada HTTP. Consultar o provedor quando houver dúvida e manter pendência se não for possível confirmar; uma solicitação antiga de mudanças não desfaz um merge já confirmado.
6. **Parâmetros propostos para G-SPEC:** reter metadados de entregas terminadas por 30 dias, expurgo diário, sem armazenar corpo bruto; nunca expurgar pendências não resolvidas automaticamente. Preservar permanentemente os fatos aplicados e o histórico funcional, separados da tabela operacional. Após expirar a entrega, um replay continua sem efeito pelo fato consumido e pela validação de estado. A política não expira `TaskEvent`, `StageHistory` nem `AuditLog`.

### RF-04 — Identificação determinística da task

1. Resolver sempre no projeto associado ao repositório, nesta ordem:
   1. Vínculo explícito previamente registrado por **`vincular_pr(id, url)`**.
   2. Branch de origem no padrão **`task/T-XXXX-slug`**, extraindo o ID completo; `XXXX` representa a sequência numérica do método, com pelo menos quatro dígitos.
   3. Marcador único **`[T-XXXX]`** no título do PR.
2. Um vínculo explícito válido prevalece mesmo se branch/título apontarem para outra task; registrar a divergência para conferência. Sem vínculo explícito, branch válida prevalece sobre título. Se a fonte prioritária trouxer ID inexistente, duplicado ou inacessível, registrar erro de vínculo; não mascarar o erro buscando uma task de menor prioridade ou de outro projeto.
3. URL do vínculo deve resolver provedor/instância/repositório permitido e número estável do PR. Não requisitar URL arbitrária fornecida pelo cliente. Um PR associa-se a uma task neste contrato; uma task pode ter vários PRs, inclusive em repositórios autorizados distintos do mesmo projeto.
4. Na fase 2, o vínculo torna-se estruturado. Preservar comentário e campo **PR** da fase 1 como apresentação compatível; não interpretar comentários livres como credencial ou vínculo explícito. A migração dos vínculos da fase 1 precisa de formato confiável e validado com o Maestro; sem isso, sinalizar pendência em vez de inventar associações.
5. Criação de branch registra o vínculo com a task, quando encontrada, e não altera status. Branch fora do padrão não gera vínculo inferido. Nenhuma identificação autoriza uma task por si só.
6. Sem candidato válido, guardar o PR como **não vinculado**, sem criar task e sem mover nada. Exibir no Prisma, dentro do projeto, repositório, PR, branch, título, motivo e ação de vincular para pessoa autorizada. Após o vínculo, reavaliar o estado atual pelas mesmas regras; não executar cegamente toda a sequência histórica.

### RF-05 — Transições observadas nas fases 2 e 3

A tabela abaixo reproduz o contrato de `prisma.md`. A origem precisa coincidir com a coluna atual; não há salto intermediário implícito. `Bloqueada`, `Backlog`, `Pronta` e `Concluida` não são abandonadas por um evento Git fora das transições previstas.

| Evento confirmado | Origem | Destino | Condição e efeito |
|---|---|---|---|
| Branch `task/T-0007-*` criada | Qualquer | Sem movimento | Somente registra o vínculo |
| PR aberto não draft ou `ready_for_review` | Em desenvolvimento | Em revisao | Task identificada e autorizada anteriormente por humano |
| PR aberto draft | Qualquer | Sem movimento | Registra PR; aguarda pronto para revisão |
| PR fechado sem merge | Em revisao | Em desenvolvimento | Confirmar `merged = false`; merge também gera evento de fechamento e não entra nesta regra |
| Review efetiva com `changes_requested` | Em revisao | Em desenvolvimento | Confirmar review vigente no ciclo atual; não usar review obsoleta |
| PR mergeado na principal configurada | Em revisao | Em teste | Confirmar merge e ausência de **qualquer outro PR aberto da mesma task**, incluindo draft, em todos os vínculos de repositório do projeto |
| PR mergeado em outra branch | Qualquer | Sem movimento | Registra o fato, sem promover para teste |
| Deploy bem-sucedido, fase 3 | Em teste | Concluida | Ambiente de conclusão e revisão implantada comprovados conforme RF-08 |

Consultar os PRs abertos relevantes antes da promoção por merge, inclusive os identificáveis por branch/título ainda não materializados como vínculo local. Se faltar permissão, paginação, orçamento de API ou acesso a um repositório relevante, não afirmar ausência de outro PR: deixar promoção pendente. Serializar processamento concorrente da mesma task e revalidar antes de gravar.

Destino já alcançado pelo mesmo fato é resultado sem movimento. Origem incompatível gera resultado explicado, não correção forçada de status. PR reaberto ou convertido novamente em draft tem seu estado atualizado, mas uma nova transição específica não está definida pelo contrato e depende do PO. Aprovação de review isolada não equivale a merge.

### RF-06 — Reconciliação periódica

1. Ler PRs recentes de cada repositório habilitado, abertos e fechados, ordenados por atualização e paginados; consultar detalhes/reviews quando necessários. Somente listar abertos perde merges e fechamentos. Usar a [API oficial de PRs](https://docs.github.com/en/rest/pulls/pulls#list-pull-requests), sem depender de buscar arquivos do método.
2. **Parâmetros propostos para G-SPEC:** execução a cada 5 minutos; janela móvel de 7 dias; sobreposição de 1 hora com o cursor da última rodada completa; páginas de até 100 itens. Além da janela, revisitar todos os PRs abertos conhecidos e vínculos com processamento pendente. Instalação inicial, indisponibilidade maior que a janela e recuperação histórica usam varredura explicitamente solicitada, sem transições retroativas irrestritas.
3. Retomar paginação/cursor após falhas; só avançar o marco concluído ao terminar a janela. Registrar início/fim, atraso, itens lidos, pendências, erros e próximo agendamento. Não considerar uma resposta parcial como uma visão completa da task.
4. Respeitar orçamento compartilhado por credencial/instalação, cabeçalhos `X-RateLimit-*`, `Retry-After` e limites secundários. A referência comum para autenticação de usuário é 5.000 requisições/hora, mas varia por credencial/plano; não fixar esse valor como cota garantida. Diante de 403 por limite ou 429, aguardar a indicação do provedor, aplicar recuo com dispersão e não tentar outro token para contornar a restrição. Ver [limites oficiais do GitHub](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).
5. **Orçamento inicial proposto:** no máximo 200 leituras por rodada por repositório, sempre subordinadas ao orçamento global; excedente continua na próxima rodada com atraso visível. Serializar leituras por credencial, aproveitar respostas condicionais quando suportadas e priorizar confirmação de eventos e PRs abertos sobre varredura histórica.
6. Reconciliação não fabrica `X-GitHub-Delivery`; grava sua própria execução e utiliza a deduplicação semântica RF-03. Webhook e job concorrentes para o mesmo merge geram um único movimento. Canal do efeito é o de quem efetivamente o aplicou; a segunda observação fica no registro operacional.
7. Se webhooks intermediários se perderem, reconstruir somente fatos comprovados e em ordem causal compatível com RF-05. Por exemplo, task em desenvolvimento com PR já mergeado pode percorrer revisão e teste apenas se abertura não draft/prontidão e merge estiverem comprovados; cada passo gera histórico próprio. Se faltarem evidências ou houver intervenção humana posterior, registrar divergência para revisão, sem inventar transição.

### RF-07 — Gate `prisma/autorizacao`, fase 3

1. Publicar no commit HEAD atual do PR um **Commit Status** de contexto `prisma/autorizacao`, ou um **Check** de mesmo nome, com link para a decisão consultável no Prisma. Escolher um mecanismo por integração para evitar nomes concorrentes. O contrato de estados do Commit Status é `pending`, `success`, `failure` e `error`, conforme a [API de statuses](https://docs.github.com/en/rest/commits/statuses).
2. Proposta de avaliação: `success` apenas quando há task vinculada sem ambiguidade, autorização humana válida para o ciclo, escopo correto e nenhuma condição de bloqueio/revogação; `failure` para autorização negada, task não vinculada ou bloqueada; `pending` durante avaliação; `error` quando não se pode determinar com segurança. Em Checks, mapear para execução em andamento e conclusão success/failure, nunca neutral/skipped para permitir merge. O significado exato de autorização, autonomia e homologação ainda exige decisão PD-04.
3. PR e evento não constituem autorização humana. Token de agente não publica o gate, não muda sua política e não revoga proteções. A aplicação verificadora recebe somente a permissão Git necessária para publicar status/check no repositório configurado.
4. Novo commit (`synchronize`), mudança de vínculo, autorização, bloqueio ou política exige reavaliação. Uma aprovação de SHA anterior não autoriza SHA novo. Gravar versão da autorização e SHA avaliados; evitar que trabalho atrasado publique sucesso para decisão revogada.
5. Administrador humano configura a principal para exigir `prisma/autorizacao`, além das verificações existentes, com origem restrita à aplicação confiável e política explícita de bypass. A [proteção de branch do GitHub](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) permite exigir status/checks e selecionar a aplicação esperada. Configurar isso é entrega futura com autorização própria; esta spec não altera o repositório.
6. Uma falha de publicação fica pendente para retry por saída persistente; não libera merge localmente. Prisma não disponível mantém o gate sem sucesso. Revogação precisa atualizar o status no provedor; indisponibilidade após sucesso anterior é risco explícito, não garantia de revogação instantânea.

### RF-08 — Deploy observado, fase 3

1. Receber `deployment_status` bem-sucedido do repositório/ambiente permitido e verificar no provedor o deployment, sua revisão e sua relação com os merges da task. Um workflow verde genérico ou o nome de um job não é prova de deploy.
2. O administrador define o ambiente que significa conclusão. Sucesso em preview/homologação não conclui a task se o ambiente configurado for produção. Falha, cancelamento ou deploy sem task identificável não movem status.
3. Correlacionar SHA efetivamente implantado aos commits resultantes dos PRs, considerando merge, squash ou rebase e o conjunto de tasks incluídas no deploy. Não comparar apenas HEAD da branch da task com SHA de produção. Registrar evidência por task; no caso de múltiplos PRs, exigir que a revisão implantada cubra os merges necessários.
4. Somente `em-teste → concluida` é automático. Repetição de sucesso não duplica histórico. Deploy não aprova G-DEPLOY, não inicia implantação e não contorna homologação exigida pelo projeto. Fonte de deploy fora dos deployments do provedor depende de contrato próprio, não de endpoint anônimo de conclusão.

### RF-09 — Caso da equipe do Detran

1. O método está fora do Git e `docs/trabalho` não é versionado. PRs não contêm ID da task no título. A integração não exige mudar essas práticas nem envia esses documentos ao provedor.
2. PR com título livre e vínculo explícito `vincular_pr(T-0007, url)` resolve T-0007, mesmo com branch fora do padrão. Sem vínculo explícito, branch `task/T-0007-corrigir-consulta` resolve a mesma task mesmo com título livre. O fallback por título permanece disponível para outras equipes, mas não é pré-requisito.
3. PR com título livre e branch fora do padrão fica visível como **não vinculado**, sem movimentação. Uma pessoa autorizada vincula no Prisma; o sistema reavalia o fato atual e registra a autoria do vínculo. Não extrair ID de caminho local, corpo de commit ou nome da HU por heurística.

### RF-10 — Auditoria e observabilidade

1. Cada transição efetiva registra organização, projeto, work item e ID do método, coluna anterior/nova, instante UTC, ator e snapshot do nome, papel, motivo, canal **`ui`**, **`mcp`**, **`webhook`** ou **`reconciliacao`**, correlação e, quando houver, repositório/PR/SHA/entrega ou rodada. O canal vem do contexto confiável da API, nunca de um campo livre do token do agente.
2. Reutilizar mecanismos de histórico/auditoria existentes, append-only e na mesma transação do movimento (D34/D47). Ator sistema identifica a integração, preservando separadamente o autor Git observado; não simular autorização humana com a identidade do autor do PR.
3. Tentativas recusadas, eventos ignorados e duplicados ficam em registro operacional/segurança com motivo; não criam um movimento fictício. Comentários padronizados de PR continuam separados do histórico automático.
4. Expor no projeto estado da integração, última reconciliação bem-sucedida, atraso, erros recuperáveis e PRs não vinculados. Segredos, tokens, corpo bruto e cabeçalhos de autenticação não aparecem em telemetria nem UI.

## Invariants

- Status da task é a coluna atual, nunca outro campo nem frontmatter duplicado.
- Tasks do método são work items de topo, sem `ParentId`; a HU é representada pelo campo **HU** e pela tag correspondente, nunca por um work item pai.
- ID do método resolve dentro do projeto e tenant; mesmo `T-0007` em dois projetos não cruza escopos.
- Agente nunca autoriza `backlog/pronta → em-desenvolvimento`, inclusive por chamada REST direta, lote, troca de coluna ou manipulação de papel/canal.
- Criar branch, abrir PR, vincular PR ou receber webhook não supre autorização humana ausente.
- Um fato aplicado gera no máximo uma transição correspondente; novo ciclo legítimo não é descartado como duplicata.
- Histórico funcional é permanente; expurgo operacional não elimina autoria, prova de autorização nem deduplicação de efeitos aplicados.
- Incerteza de identidade, autorização, evidência ou completude dos PRs impede promoção automática e gera pendência visível.
- Nenhuma regra desta spec autoriza o Prisma a criar branch, fazer merge, implantar ou aprovar human gates.

## User Story References

- Intenção humana fornecida na tarefa 8: como responsável pelo método, acompanhar autorização, revisão, teste e conclusão no Prisma com evidência Git e sem depender de arquivos versionados ou títulos padronizados no Detran.
- Fontes funcionais: `prisma.md`, seções “Operações do MCP”, “O que o webhook move” e “Fases de implantação”; `fluxo.md`, seções “Autorização”, “Entrega e autonomia” e “Quem move o status”; `local.md`, separação entre os modos.
- Não foi encontrada história canônica específica desta integração em `stories/`. Criar/vincular a história e seu ID no catálogo é pendência para o Maestro antes da implementação; este draft não inventa um ID aprovado nem altera histórias existentes.

## Acceptance Criteria

1. **CA-01 — Given** integração GitHub válida, **When** chega corpo original com assinatura correta, **Then** a entrega é persistida e aceita; alteração de um byte, assinatura ausente/malformada ou segredo de outro repositório é recusado sem movimento.
2. **CA-02 — Given** mesma entrega autenticada, **When** ocorre reenvio sequencial ou concorrente, **Then** há uma entrada operacional e no máximo um efeito; mesmo ID com corpo divergente é conflito.
3. **CA-03 — Given** queda antes/depois do commit do movimento, **When** o processamento é retomado, **Then** status, histórico e consumo do fato permanecem consistentes, sem perda ou duplicação.
4. **CA-04 — Given** vínculo explícito T-0001, branch T-0002 e título T-0003, **When** o PR é identificado, **Then** prevalece T-0001 e a divergência fica registrada; sem explícito, prevalece a branch.
5. **CA-05 — Given** fonte prioritária inválida, ID duplicado ou pertencente a outro projeto, **When** resolver a task, **Then** nenhuma task de fallback/outro escopo é movimentada e o erro fica visível.
6. **CA-06 — Given** task em Backlog ou Pronta, **When** token de agente tenta iniciar por MCP, REST direto ou movimentação de coluna, **Then** a API nega e registra a tentativa; mensagem dizendo que um humano pediu não altera o papel autenticado.
7. **CA-07 — Given** task bloqueada pelo próprio agente durante execução autorizada e resposta humana registrada, **When** o mesmo agente retoma, **Then** a API permite o retorno; bloqueio alheio ou criado para contornar autorização é recusado.
8. **CA-08 — Given** task identificada em qualquer coluna, **When** cria a branch correspondente, **Then** o vínculo é registrado sem movimento; branch fora do padrão não cria vínculo inferido.
9. **CA-09 — Given** task autorizada em desenvolvimento, **When** abre PR não draft ou torna draft pronto, **Then** vai uma vez para revisão; abertura draft isolada não move.
10. **CA-10 — Given** task em revisão, **When** fecha PR sem merge ou há changes requested vigente, **Then** volta ao desenvolvimento; fechamento de PR mergeado não aplica essa regra.
11. **CA-11 — Given** task em revisão com todos os repositórios relevantes consultados, **When** mergeia na principal sem outro PR aberto, **Then** vai para teste; outro PR aberto/draft, merge fora da principal ou leitura incompleta impedem a promoção.
12. **CA-12 — Given** task bloqueada/concluída ou intervenção humana posterior, **When** chega evento atrasado de revisão/merge, **Then** não há salto ou regressão incompatível e o motivo fica registrado.
13. **CA-13 — Given** webhook perdido dentro da janela ou PR aberto conhecido fora dela, **When** a reconciliação encontra evidência, **Then** aplica apenas a sequência comprovada; concorrência com webhook não duplica movimentos.
14. **CA-14 — Given** janela paginada, erro ou rate limit, **When** a rodada é interrompida, **Then** preserva cursor, respeita espera/orçamento e apresenta atraso; não declara ausência de outros PRs com leitura parcial.
15. **CA-15 — Given** entrega operacional expirada, **When** o mesmo fato reaparece, **Then** o histórico permanente e o consumo semântico impedem reaplicação; pendências não são apagadas pelo expurgo.
16. **CA-16 — Given** repositório Detran com documentos não versionados e título livre, **When** há vínculo explícito ou branch no padrão, **Then** identifica a task sem consultar arquivos do método nem exigir ID no título.
17. **CA-17 — Given** PR sem vínculo explícito, sem branch no padrão e sem marcador no título, **When** é observado, **Then** aparece como não vinculado sem movimento; ao vincular na UI, reavalia o estado atual com autoria e escopo.
18. **CA-18 — Given** fase 3 habilitada e política aprovada, **When** o commit é avaliado, **Then** `prisma/autorizacao` reflete a decisão daquela task/SHA; task não vinculada, bloqueio, recusa ou indisponibilidade não produzem sucesso.
19. **CA-19 — Given** sucesso no SHA anterior, **When** há novo commit ou revogação, **Then** ocorre nova avaliação/publicação; token do agente não publica sucesso nem configura bypass.
20. **CA-20 — Given** principal protegida em repositório de teste, **When** falta gate bem-sucedido da aplicação confiável, **Then** merge fica impedido apesar de outros checks verdes; o Prisma não faz o merge.
21. **CA-21 — Given** task em teste, **When** deployment de sucesso comprova seus merges no ambiente configurado, **Then** conclui uma vez; preview, falha, SHA sem correlação ou ambiente errado não concluem.
22. **CA-22 — Given** transição por UI, MCP, webhook ou reconciliação, **When** consultar histórico autorizado, **Then** há antes/depois, autor/papel, canal confiável e correlação; tentativa recusada não gera movimento fictício nem revela segredos.
23. **CA-23 — Given** projeto/tenant diferente ou token revogado, **When** tenta vincular, consultar não vinculados, mover ou publicar, **Then** não obtém acesso nem altera recurso fora do escopo.
24. **CA-24 — Given** GitLab em versão suportada, **When** recebe MR/evento autenticado ou reentrega, **Then** o adaptador normaliza os mesmos fatos e respeita identidade, matriz e idempotência, sem reutilizar indevidamente os cabeçalhos GitHub.

## Data Impact

Modelo **proposto**, sem criação de entidades, SQL, migration ou alteração do snapshot nesta entrega:

| Registro lógico | Campos e restrições propostas |
|---|---|
| `GitRepositoryIntegration` | Organização, projeto, quadro, provedor/instância, repository ID, principal, ambientes, habilitação por fase, referência ao segredo/credencial externa e versão da política; nenhuma credencial em claro |
| `MethodStageMapping` | Integração/quadro, status do método e `StageId`; associação inequívoca e pertencimento ao quadro |
| `TaskGitLink` | Projeto, work item, repositório, PR/número ou branch, origem explícita/branch/título, autor, data; identidade estável de PR única no escopo; vários vínculos por task |
| `GitPullRequestObservation` | Repositório/PR, branch/base, draft/aberto/mergeado, HEAD/merge SHA, atualização observada, task opcional, conflito ou motivo de não vínculo |
| `GitWebhookDelivery` | Integração/provedor/delivery ID único, tipo/ação, hash do corpo, instantes UTC, estado, tentativas e correlação; retenção operacional RF-03 |
| `GitAppliedFact` | Fato/versionamento do PR/deploy, task, transição e histórico correlato; unicidade semântica persistente para RF-03; sem substituir auditoria |
| `GitReconciliationRun` | Integração, janela/cursor, resultado, orçamento usado, erro, atraso e continuidade |
| `MethodAuthorization` | Referência à autorização humana do ciclo, escopo, ator, concessão/revogação e versão; reutilizar representação da fase 1 se equivalente |
| `GitStatusPublication` | Repositório/PR/SHA, decisão e versão, contexto, estado de publicação e retentativas; saída durável para RF-07 |
| Histórico existente | Referência ao fato/entrega/rodada, papel e canal confiáveis; preservar D34/D47 e retenção permanente |

O campo **ID do metodo** (nome literal, sem acento) é uma definição personalizada no projeto e um valor na task. Exigir unicidade por projeto e busca inequívoca sem substituir o ID interno do work item. Cada task é um work item de topo, com `ParentId` nulo. A referência à HU fica no valor do campo personalizado de projeto **HU**, por exemplo **HU-0001**, e numa tag com o mesmo nome; não criar work item para a HU nem usar subitens para esse vínculo. O campo **PR** e o comentário da fase 1 não suportam sozinhos múltiplos PRs nem unicidade; projetar apresentação a partir dos vínculos estruturados após aprovar a compatibilidade.

Implementar este modelo provavelmente exigirá migrations aditivas; o desenho final, índices, backfill e estratégia de retenção precisam de **G-MIGRATION**. Mudança na estrutura imutável de auditoria exige **G-HISTORY**. Nada é aprovado por esta proposta.

## Authorization Impact

Papel do método (`humano`, `agente`, `sistema`) é distinto do perfil funcional Prisma. A permissão efetiva é a interseção entre papel, projeto, permissões existentes, estado e autonomia configurada. Visualizador não vira escritor por possuir token humano (D17/D18/D55/D58).

| Operação/transição | Humano autorizado | Agente | Sistema de integração |
|---|---|---|---|
| Ler regras/tasks no escopo | Sim | Sim | Somente o necessário à integração |
| Criar task e comentar/vincular PR | Conforme permissões | Conforme contrato da fase 1, sem autorização implícita | Registrar observações/vínculos inferidos; não criar tasks |
| Backlog → Pronta | Sim | Não | Não |
| Backlog/Pronta → Em desenvolvimento | **Somente humano** | **Nunca**, mesmo com instrução textual humana | Não; branch/PR não autoriza |
| Execução → Bloqueada | Sim | Sim, no escopo permitido, com motivo/autoria | Não por evento Git; erro operacional não muda coluna |
| Bloqueada → Em desenvolvimento | Sim | Só bloqueio próprio, originado em execução já autorizada, após resposta humana registrada | Não |
| Em desenvolvimento → Em revisao | Sim | Apenas nível 0 do modo Prisma | PR não draft/pronto confirmado |
| Em revisao → Em desenvolvimento | Sim | Não como compensação de Git | Fechado sem merge ou changes requested |
| Em revisao → Em teste | Sim | Não para reproduzir merge no modo Prisma | Merge na principal, condição de múltiplos PRs RF-05 |
| Em teste → Concluida | Sim, conforme aceite/homologação | Não para reproduzir deploy no modo Prisma | Deploy comprovado na fase 3 |
| Configurar integração/política/segredos/proteção | Administrador com autoridade correspondente | Não | Sem autoelevação ou autoaprovação |

O contrato específico `prisma.md` prevalece para transições comprovadas por Git: o agente comenta e aguarda; não compensa webhook perdido movendo status. A permissão genérica de nível 2 de `fluxo.md` não libera esse atalho em modo Prisma. A reconciliação entre conclusão sem homologação do fluxo genérico e a tabela específica merge → teste → deploy → concluída está em PD-04.

**Requisito da API:** hoje a restrição de início está apenas no adaptador. A API deve obter papel de credencial autenticada, negar falsificação de papel/canal e aplicar a matriz em qualquer escrita da coluna da task, inclusive endpoints genéricos, lote e caminhos indiretos. Não basta proteger `mover_status` no MCP. Retomada de bloqueio não pode permitir o caminho Backlog → Bloqueada → Em desenvolvimento para iniciar sem autorização.

Tokens de agente têm escopo de projeto, validade, revogação e armazenamento seguro; não podem emitir token humano/sistema. Sistema usa identidade dedicada restrita à integração e fatos verificados, sem login de pessoa emprestado. A credencial Git deve permitir leitura dos PRs/reviews/deployments e, somente na fase 3, escrita de status ou checks; sem permissão de alterar conteúdo, dar merge ou administrar proteção. Política de emissão, autorização da fase 1 e compatibilidade com D91 exigem validação PD-02/PD-03.

## Contracts

Contratos externos propostos, a detalhar em DTOs somente após aprovação:

| Entrada/saída | Contrato |
|---|---|
| Webhook GitHub | `POST /api/integrations/git/github/{integrationId}/webhook`; corpo original; `X-Hub-Signature-256`, `X-GitHub-Delivery`, `X-GitHub-Event`; validação RF-02 |
| Webhook GitLab | `POST /api/integrations/git/gitlab/{integrationId}/webhook`; autenticador específico da versão suportada; normalização para branch/PR/review/deploy do mesmo domínio |
| Operações da fase 1 | Preservar `regras_do_projeto`, `obter_task`, `listar_tasks`, `proximas_tasks`, `criar_task`, `mover_status`, `comentar` e `vincular_pr`; API passa a ser autoridade de permissão |
| `regras_do_projeto()` | Deve expor matriz efetiva, autonomia, convenções, mapeamento de status e capacidades habilitadas, sem segredos |
| Vínculo explícito | ID do método no projeto e URL canônica do PR; resposta com task interna, PR e origem do vínculo; reenvio idêntico é idempotente; vínculo conflitante exige resolução autorizada |
| PRs não vinculados | Consulta paginada e ação de vínculo no contexto do projeto, reaproveitando a autorização de `vincular_pr`; rotas concretas de UI/API pendentes |
| Publicação GitHub, fase 3 | Commit Status no SHA com contexto `prisma/autorizacao`, estado, descrição curta sem segredos e URL de detalhes protegida; ou Check equivalente conforme PD-04 |

Desenho GitLab: normalizar Push de criação de branch, Merge Request aberto/pronto/fechado/mergeado, review equivalente e deployment. Não confundir aprovação de MR com merge nem assumir um evento idêntico a `changes_requested`: o suporte depende da versão escolhida. Para versões com assinatura, usar `webhook-signature`, `webhook-id` e `webhook-timestamp`, HMAC-SHA256 do formato Standard Webhooks e comparação em tempo constante. Deduplicar por `webhook-id` (ou `Idempotency-Key`, quando aplicável). Instâncias legadas com `X-Gitlab-Token` precisam de decisão explícita de compatibilidade; token de cabeçalho não é HMAC. Não fazer downgrade automático após assinatura inválida. Conferir versão suportada e equivalência funcional antes de habilitar, conforme a [documentação de webhooks do GitLab](https://docs.gitlab.com/user/project/integrations/webhooks/).

Todos os eventos são normalizados com provedor, integração, ID do repositório, tipo do fato, PR/branch/deploy, revisão/instante do provedor e correlação; a máquina de estados não depende dos nomes dos cabeçalhos. GitLab aplica a mesma política de não vinculados e não exige título padronizado.

## Dependencies

- Fase 1 do Maestro: identidade de tokens, operações de método e formato do vínculo explícito. Esta spec não presume acesso a outra branch nem modifica `tools/prisma-mcp`.
- `AGENTS.md`; D17/D18/D34/D36/D47/D55/D58/D62/D65/D89/D91/D97; [permissões](user-access-permissions.md), [histórico](audit-leadtime-history.md) e [colunas independentes](independent-board-columns.md).
- APIs GitHub/GitLab e credencial de integração com privilégios mínimos; rotina agendada e saída persistente usando a infraestrutura que for aprovada, sem escolher nova biblioteca nesta revisão.
- História canônica, G-SPEC e, para implementação, G-MIGRATION/G-WORKFLOW/G-HISTORY quando aplicáveis. Ativação institucional e mudanças de proteção exigem autorização humana própria.

## Error Cases

| Caso | Resposta ou tratamento proposto |
|---|---|
| Assinatura ausente/inválida ou integração desabilitada | 401/403, sem processamento e sem detalhe de segredo |
| Repositório/instalação fora da configuração | 403, sem vínculo/movimento |
| Cabeçalhos necessários ausentes ou JSON inválido após autenticar | 400; tamanho excedido 413; não gerar 500 de regra |
| Recebimento válido persistido | 202; indica aceite durável, não que task foi movida |
| Entrega autenticada repetida | 200, com resultado idempotente; não bloquear retry interno pendente |
| Mesmo delivery ID com hash divergente | 409 e registro de segurança; não sobrescrever original |
| Falha de persistência antes do aceite | 503; não declarar entrega recebida |
| Task ausente/ambígua, coluna removida ou origem incompatível | Aceite do webhook preservado; resultado de processamento pendente/ignorado com motivo visível; sem movimento forçado |
| Papel sem permissão ou tentativa de início pelo agente | 403 nas operações autenticadas; conflito de estado/vínculo 409; mensagem em pt-BR |
| Consulta autenticada fora do tenant | Sem exposição do recurso; seguir padrão 403/404 do projeto |
| Timeout, 5xx ou limite do provedor | Retry com recuo, cursor preservado e atraso visível; não promover com leitura incompleta |
| Falha ao publicar gate | Saída pendente/erro; sem sucesso presumido e sem merge pelo Prisma |

## Pending Decisions

Estas perguntas são para o PO/Maestro antes da implementação; não bloqueiam a entrega documental solicitada nem representam aprovação por agente.

1. **PD-01 — História e workflow:** qual HU/ID canônico desta integração e qual quadro será o do método? Aprovar mapeamento das sete colunas para categorias e efeito de conclusão em descendentes, sem alterar D62/D89. O mapeamento de status para nomes já foi decidido; categorias e efeitos não foram.
2. **PD-02 — Papéis e fase 1:** confirmar formato persistido de `vincular_pr`, comentário/campo PR e múltiplos vínculos; quem emite token humano/agente/sistema e como registrar a resposta humana que autoriza retomar bloqueio? Como tratar repository ID compartilhado por vários projetos, se necessário?
3. **PD-03 — D91:** o MCP do método terá contrato específico de escritas diretas autorizadas por papel, distinto do MCP de IA com escritas como sugestões? A tarefa pede essa matriz, mas D91 mantém aprovação humana de escritas via IA. Registrar a compatibilidade/decisão em G-SPEC/G-SCOPE antes de implementar; não ampliar a autonomia do assistente existente.
4. **PD-04 — Fase 3:** Commit Status ou Checks, tipo de credencial/aplicação e emissor obrigatório? Qual autorização humana/ciclo libera o gate, quais bloqueios a revogam e que regras de autonomia/homologação aplicam? Confirmar merge → em-teste e deploy → concluida mesmo onde o fluxo genérico de nível 2 permite concluir sem homologação; a tabela do rastreador foi preservada neste draft.
5. **PD-05 — Operação e retenção:** aprovar ou ajustar 5 minutos, janela de 7 dias com sobreposição de 1 hora, 200 leituras por rodada/repositório e 30 dias de entregas terminadas; prazo de rotação, limite de corpo e alertas de atraso. Definir política de recuperação após indisponibilidade maior que a janela.
6. **PD-06 — Eventos incompletos/múltiplos PRs:** se um merge ficou retido por outro PR aberto e esse outro fecha sem merge, vale primeiro o retorno para desenvolvimento ou uma promoção posterior a teste pelo merge anterior? Definir também PR reaberto, retorno a draft, múltiplas reviews contraditórias e vínculo tardio após intervenção humana; não inventar novas transições.
7. **PD-07 — Deploy:** qual ambiente representa produção/aceite, qual fonte de deploy e como comprovar revisão para squash/rebase, vários repositórios e homologação obrigatória? Rollback de produção reabre task ou só registra fato? Nenhuma reabertura automática foi definida.
8. **PD-08 — GitLab:** versões/edições suportadas, aceitação de token legado e equivalência exata de pedido de alterações/gate/branch protection. Não habilitar integração parcialmente equivalente sem tornar limitações explícitas.

## Test Gate Mapping

Plano futuro; nenhum teste ou ambiente é executado por esta entrega de documentação.

| Camada e suíte proposta | Provas obrigatórias | Critérios |
|---|---|---|
| xUnit: assinatura e contratos de provedores | Bytes originais/Unicode, corpo adulterado, tamanho/formato, segredo errado, cabeçalhos, rotação, validação GitLab específica; inspeção de uso de comparação criptográfica em tempo constante, sem teste frágil de duração | CA-01, CA-24 |
| xUnit + integração SQL isolada | Unicidade concorrente, transação/queda, retry, fato permanente, expurgo operacional sem apagar auditoria, mesmo delivery ID divergente e ciclos legítimos repetidos | CA-02, CA-03, CA-15 |
| xUnit: resolver e autorização da API | Todas as prioridades, IDs duplicados, projeto/tenant, vínculo tardio, token revogado, início negado também por endpoints genéricos/lote e retomada própria/alheia | CA-04 a CA-07, CA-16, CA-17, CA-23 |
| xUnit: máquina de estados | Cada linha RF-05, draft, merge em outra base, vários PRs inclusive draft e outros repos, concorrência, review antiga e intervenção humana; pendências PD-06 ganham casos após decisão | CA-08 a CA-12 |
| xUnit: reconciliação com provedor simulado | Relógio controlado, paginação, janela/fora da janela, cursor, eventos perdidos, webhook concorrente, 403/429/5xx, orçamento compartilhado e leitura incompleta | CA-11, CA-13, CA-14 |
| xUnit: gate e deployment | Decisão por SHA/versão, publicação atrasada, falha/retry, revogação, ambiente e correlação de merges/revisão implantada | CA-18, CA-19, CA-21 |
| xUnit + integração: histórico | Antes/depois, identidade/papel/canal confiáveis, uma gravação por efeito, append-only, ausência de segredos e isolamento das consultas | CA-03, CA-22, CA-23 |
| Playwright desktop/mobile: jornada do método | Criar projeto/quadro/tasks próprios via API, simular webhook assinado no backend, observar coluna após reload, draft/pronto/changes requested/merge/deploy e histórico; testar negação com token de agente real do ambiente | CA-06, CA-08 a CA-13, CA-21, CA-22 |
| Playwright desktop/mobile: Detran e não vinculados | Título livre, nenhuma dependência de documentos versionados, resolução por vínculo/branch, lista de não vinculados, vínculo pela UI com teclado e persistência | CA-04, CA-16, CA-17, CA-23 |
| Integração de provedor isolada | Repositório descartável com aplicação de teste: proteção exige origem correta e gate, novo SHA invalida sucesso anterior; merge/deploy somente se autorizados para esse ensaio, nunca no repositório de produção | CA-18 a CA-20, CA-24 |

Os testes Playwright criam dados únicos, não dependem do seed, sincronizam por resposta/estado e limpam apenas suas fixtures. O gate de proteção real não pode ser declarado comprovado somente com mock de API: sem repositório descartável autorizado, esse critério fica explicitamente não verificado. Antes de código, resolver os gates de spec, workflow, schema e histórico aplicáveis; depois executar build .NET/xUnit, `npx tsc -b`, lint/Vitest/build e E2E conforme AGENTS.

## Risks

- Aprovação aparente por criação de branch, vínculo ou papel declarado pelo cliente; mitigar com autorização central da API e teste de todos os caminhos de escrita.
- Exceção de retomada de bloqueio usada para iniciar trabalho não autorizado; exigir proveniência da execução e resposta humana verificável.
- Perda/reordenação de webhooks, múltiplos PRs e polling parcial causarem regressão ou conclusão precoce; usar fatos consumidos, validação de origem, concorrência por task e pendências explícitas.
- Credencial/segredo vazado ou URL de vínculo usada para SSRF; usar referências externas, hosts/repositórios permitidos, revogação, mascaramento e privilégios mínimos.
- Crescimento do histórico permanente e varreduras caras; separar retenção operacional, índices e cursor sem apagar prova de efeito aplicado.
- Sucesso de gate já publicado continuar no GitHub durante queda que impede publicar revogação; avaliar prazo de validade, detecção de indisponibilidade e procedimento operacional antes da fase 3.
- Pessoas com bypass ou credenciais capazes de forjar o contexto contornarem o gate; restringir emissor e revisar permissões/proteções com o administrador.
- Deploy/squash/rebase mal correlacionados concluírem task errada; exigir evidência verificável e não promover por nome do workflow.
- Contrato externo evoluir ou GitLab não ter equivalência completa; versionar capacidades e registrar limitações, sem degradar autenticação silenciosamente.
- Confusão entre autorização do método e aprovação de ações do assistente IA (D91); decisão explícita antes de permitir escritas.

## Rollback

Desabilitar consumo e agendamento por integração, preservar entregas pendentes, vínculos e histórico e mostrar que a automação está suspensa. Não mover tasks de volta automaticamente nem apagar fatos consumidos. Retomada começa por reconciliação controlada.

Na fase 3, suspender publicação não deve remover a proteção de branch automaticamente. Procedimento de contingência/bypass pertence ao administrador humano, é auditado e exige autorização própria. Reversão de schema futuro depende de G-MIGRATION e preservação de dados. Nesta revisão não há alteração operacional a reverter.

## Traceability

- Tarefa 8 do PO (2026-10-16) → contrato recebido `prisma.md`/`fluxo.md`/`local.md` → **SPEC-METODO-GIT-INTEGRATION (draft)** → história e tasks de implementação a cadastrar pelo Maestro → CA-01 a CA-24 → suítes propostas → commits futuros.
- Fase 1: adaptador `tools/prisma-mcp` em trabalho separado; fases 2 e 3: esta proposta, sujeita a G-SPEC e decisões pendentes.
- Bases internas: `AGENTS.md`, `DECISIONS.md`, `ROADMAP.md`, `context/index.yaml` e specs relacionadas em Dependencies. Este documento não altera o roadmap nem atribui aprovação a fase ainda não registrada.
- Referências técnicas públicas consultadas em 2026-10-16 estão vinculadas em RF-02/RF-03/RF-06/RF-07 e Contracts; nenhum segredo ou conteúdo privado foi enviado às consultas.
