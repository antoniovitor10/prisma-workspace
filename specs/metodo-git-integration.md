# SPEC-METODO-GIT-INTEGRATION: Método de trabalho e integração Git

**Status:** review

**Data:** 2026-10-16
**Origem:** tarefas 8 e 9 do PO, contrato do rastreador Prisma e decisões D100/D101.
**Revisão:** correções da REVISAO git-spec do Prumo sobre cb67792; 2a autorizada em escopo pelo GOALS. Adoção dos parâmetros/regras PD-01/05/06, estado de G-SPEC/G-MIGRATION e nova revisão precisam de registro humano antes do código; PD-04/07 pertencem à fase 3.
**Entrega desta revisão:** somente documentação corrigida, para git-spec-2. Não autoaprova decisões/gates nem substitui a nova REVISAO exigida pelo GOALS. A autorização geral da fase 2a está registrada no GOALS; a confirmação dos parâmetros pendentes é responsabilidade do Maestro/PO, conforme a seção Pending Decisions.

## Objective

Especificar as entregas 2a, 2b e 3 da integração do método de trabalho com o Prisma: observar GitHub, associar branches e PRs às tasks, aplicar transições autorizadas e publicar a autorização de merge. GitLab fica fora do primeiro ciclo, com provedor plugável (D101). O Prisma é a fonte de verdade do status; o conteúdo da HU, PRD e task permanece nos documentos do método, inclusive quando esses documentos ficam fora do Git.

## Context

A fase 1 foi entregue pelo Maestro na base desta branch (D100): adaptador fino em TypeScript, em `tools/prisma-mcp`, para status, comentários, vínculo de PR e importação. Hoje aceita login/senha ou JWT e aplica papel localmente; esta revisão especifica sua migração para tokens emitidos pela API, sem alterar código nesta tarefa.

A D101 decide GitHub, inclusive para a equipe do DETRAN, GitHub App como credencial e implantação em três entregas. Também resolve a relação com a D91: o MCP externo do método escreve diretamente segundo o papel do token; o assistente de IA interno continua gerando sugestões que exigem aprovação humana. São contratos separados, sem herança de autonomia entre eles. A D101 não aprova automaticamente G-SPEC nem G-MIGRATION.

As fontes recebidas são `prisma.md`, `fluxo.md` e `local.md`, na pasta de handoff `~/projetos/prisma-handoff/metodo/`. São referências de planejamento externas ao repositório; os requisitos necessários à revisão estão descritos abaixo, sem depender de acesso a essa pasta na implantação.

Mapeamento já decidido pelo PO (D100 e atualização do modelo):

| Conceito do método | Representação no Prisma |
|---|---|
| Task `T-0001` | Work item de topo do quadro, sem `ParentId`, com valor no campo personalizado de projeto **ID do metodo** (sem acento); ID único dentro do projeto, não global |
| HU `HU-0001` | Valor no campo personalizado de projeto **HU** da task e tag com o mesmo nome, por exemplo **HU-0001**; a HU não vira work item |
| Status | Colunas **Backlog**, **Pronta**, **Em desenvolvimento**, **Em revisão**, **Em teste**, **Concluída**, **Bloqueada** |
| PR na fase 1 | Comentário padronizado e campo personalizado **PR**, gravados por `vincular_pr` |

Os nomes da API do método usam `backlog`, `pronta`, `em-desenvolvimento`, `em-revisao`, `em-teste`, `concluida` e `bloqueada`. A integração resolve cada um para um `StageId` configurado no quadro, mantendo a coluna como status canônico (D65/D89); não cria um segundo status no work item. Não converte automaticamente essas sete colunas em categorias abertas/concluídas: esse mapeamento precisa ser validado antes da implementação.

Atualização do PO em 2026-10-16: as tasks ficam no topo para aparecerem no Kanban, cuja consulta lista itens sem pai (`WorkItemRepository.cs`, filtro por `ParentId`). O vínculo com a HU usa o campo **HU** e a tag, sem hierarquia de work items.

No modo local, status e histórico ficam no frontmatter da task. No modo Prisma, as tasks não têm `status:` e não há escrita de volta nos arquivos. O status próprio da HU permanece em seu documento, conforme o contrato; mover uma task não conclui automaticamente a HU.

## Scope

| Entrega, nesta ordem | Incluído | Critérios de aceite |
|---|---|---|
| **2a — Papéis na API e webhook GitHub** | Tokens humano/agente/sistema por projeto, validade/revogação e emissão pela UI por admin do projeto; matriz de agente/sistema em qualquer canal, inclusive UI autenticada com esse token e move/update genéricos; regressão humana UI inalterada; cadastro do repositório/GitHub App; HMAC, fila durável e idempotência; vínculo explícito > branch > título; transições de PR e lista/ação de PRs não vinculados; migração MCP; auditoria | CA-2A-01 a CA-2A-18 |
| **2b — Reconciliação** | Leitura periódica de PRs recentes e pendentes, janela/cursor, recuperação de perdas, rate limit e concorrência com webhook | CA-2B-01 a CA-2B-04 |
| **3 — Gate e deploy observado** | `prisma/autorizacao` no commit, proteção da principal e conclusão por deploy comprovado | CA-3-01 a CA-3-04 |

**Compatibilidade obrigatória do GOALS:** sessões humanas interativas da UI continuam criando e movendo cards segundo as permissões e validações já existentes em **todos** os projetos, com ou sem método. A nova matriz de transições e as regras anti-contorno restringem somente principais agente/sistema. Tokens do método, inclusive os de papel humano, não são sessões interativas e não podem conceder autorização humana de início ou liberação de retomada; essa distinção não introduz uma matriz nova para a pessoa na UI.

2a entrega um fluxo útil sem agendamento de reconciliação e sem gate/deploy. Pode consultar o GitHub para verificar um evento ou listar PRs abertos da task; isso não é a varredura periódica da 2b. Sua fila de entrega deve ter retry durável desde o primeiro corte. 2b depende dos contratos e fatos persistidos da 2a; 3 depende da autorização e observação já consolidadas. Cada corte tem habilitação e validação próprias; não antecipar tabelas/workers da fase seguinte sem nova revisão.

O modelo de dados e o plano de execução abaixo são propostas revisáveis para G-MIGRATION/G-SPEC, não implementações ou aprovações.

## Out of Scope

- Implementar código, schema, migrations, workers, workflows de CI, tokens, webhook real ou proteção de branch nesta tarefa.
- GitLab no primeiro ciclo; preservar a fronteira do provedor sem entregar endpoint, credencial ou testes de integração GitLab nas entregas 2a/2b/3.
- Criar branches, abrir PRs, fazer merge ou iniciar deploy pelo Prisma. Essas operações continuam no provedor e nas ferramentas do executor.
- Versionar documentos do método contra a configuração da equipe, exigir ID no título ou ler arquivos locais do desenvolvedor a partir do servidor.
- Substituir a fase 1, importar automaticamente todo o histórico Git ou manter status em arquivos e Prisma ao mesmo tempo.
- Concluir HU automaticamente, mudar regras de descendência/categorias ou aprovar human gates por automação.

## Functional Requirements

### RF-01 — Cadastro e vínculo de repositório

1. Cada integração identifica organização, projeto, quadro do método, provedor, instância, ID estável do repositório, principal configurada, GitHub App/instalação e referências externas aos segredos. Renomear repositório não altera sua identidade. Não presumir que a principal se chama `main`.
2. O vínculo exige permissão administrativa no escopo. Uma entrega não pode escolher seu tenant/projeto por um campo arbitrário do payload. Nesta proposta, cada integração tem um projeto destinatário inequívoco; repositórios compartilhados por vários projetos ficam pendentes de desenho próprio.
3. Associar os sete status a IDs de colunas existentes e validar pertencimento ao quadro. Coluna ausente, excluída ou associação ambígua suspende as transições afetadas, sem criar coluna ou adivinhar destino. Configuração de categoria e efeitos na descendência segue D62/D89, com decisão pendente indicada ao final.
4. **D101 — credencial definida:** usar GitHub App instalada com acesso ao repositório autorizado. Chave privada e segredo HMAC ficam em cofre/configuração externa; persistir somente referências, App ID, Installation ID e Repository ID. Gerar installation access token no backend, restrito aos repositórios/permissões necessários, e renovar segundo sua expiração, sem login de usuário ou PAT. Ver [autenticação da instalação](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/authenticating-as-a-github-app-installation).
5. Leitura de PRs/reviews/deployments e metadados necessários é o limite da credencial deste ciclo; escrita de checks/status apenas na fase 3. Não conceder escrita de conteúdo, merge ou administração de proteção à aplicação. Cadastro valida que a instalação realmente acessa aquele Repository ID; remoção/suspensão do acesso interrompe promoções e aparece como erro operacional.

### RF-02 — Entrada GitHub autenticada

1. Propor `POST /api/integrations/git/github/{integrationId}/webhook`, via HTTPS, com corpo original preservado até validar a assinatura. O ID identifica a configuração; não é uma credencial.
2. Calcular HMAC-SHA256 dos bytes originais com segredo aleatório exclusivo do repositório; validar `X-Hub-Signature-256`, prefixo `sha256=`, tamanho e hexadecimal, comparando os digests em tempo constante. Não reserializar JSON nem aceitar SHA-1 como substituto. Essa verificação segue a [documentação de assinatura do GitHub](https://docs.github.com/en/webhooks/using-webhooks/validating-webhook-deliveries).
3. O segredo fica em configuração externa/cofre, separado dos tokens Prisma e da credencial de leitura/escrita Git. Não aparece no Git, URL, histórico, logs ou resposta. Rotação pode aceitar a versão anterior por prazo configurado e auditado, nunca indefinidamente.
4. Após autenticar, conferir ID do repositório e instalação autorizada; validar cabeçalhos de evento e entrega. Limitar tamanho do corpo e requisições por integração. Payload válido de outro repositório não pode gerar vínculo nem movimento.
5. Aceitar apenas eventos necessários: `create` de branch, `pull_request` nas ações pertinentes, `pull_request_review` para mudanças solicitadas e, na fase 3, `deployment_status`. `ping` verifica conectividade sem mover tarefas. Eventos autenticados fora do escopo são registrados como ignorados.
6. Persistir a entrada de forma durável antes de responder sucesso e processar fora da requisição. Falha ao persistir não recebe confirmação de aceite. Falhas posteriores são retomadas pela fila persistente e pela reconciliação; não presumir reentrega automática do provedor. O ingresso anônimo resolve por ID somente os metadados mínimos da integração com `IgnoreQueryFilters` para autenticar HMAC; não aceita organização/projeto do payload como autoridade. Só após validar assinatura, repositório e instalação estabelece o tenant persistido da integração. O worker descobre pendências por metadados mínimos, abre **novo escopo DI por integração/entrega** e estabelece organização/projeto ativos, principal sistema da integração, origem `webhook` e correlação da entrega **antes** de carregar entidades filtradas ou escrever. Consulta global de configuração/inbox não autoriza consulta global de tasks. Contextos HTTP não são reutilizados no worker.
7. Para manter segredo **por repositório**, a proposta de instalação usa webhook de repositório configurado por seu administrador humano, além da GitHub App usada como credencial de API; não dar administração de webhooks à App somente para automatizar esse cadastro. Não presumir segredo por instalação no webhook global da App: esse webhook é único por App, conforme os [tipos de webhook do GitHub](https://docs.github.com/en/webhooks/types-of-webhooks). A alternativa de webhook global e sua política de segredo precisam de aprovação específica; não trocar silenciosamente o isolamento já previsto.

### RF-03 — Idempotência, concorrência e retenção

1. Manter tabela de entregas com unicidade por integração, provedor e `X-GitHub-Delivery`. Reentrega reconhecida retorna sucesso sem reaplicar efeitos. Mesmo ID com conteúdo divergente é conflito, não uma nova operação. Validar assinatura inclusive para IDs já conhecidos. O GitHub preserva o ID em reentregas, conforme suas [boas práticas de webhooks](https://docs.github.com/en/webhooks/using-webhooks/best-practices-for-using-webhooks).
2. Registrar estados recebido, processando, processado, ignorado e falha recuperável/definitiva, tentativas, correlação, hash do corpo e motivo do resultado. Recebido não significa processado; uma falha não pode tornar o evento permanentemente descartado como duplicata.
3. Entrega e reconciliação passam pela interface transacional **`IMethodMoveService`**, congelada em F0 e implementada/integrada pela Forja em F1, e pela mesma trava/versão de concorrência por task. A Ponte consome essa interface; não duplica a gravação de coluna/StageHistory/TaskEvent no worker. Na transação local, validar origem atual, aplicar movimento e histórico e registrar o efeito consumido. Uma queda antes do commit não deixa movimento parcial; depois do commit, uma retomada não duplica histórico.
4. A deduplicação semântica distingue reentrega de nova mudança legítima: usa repositório, PR, fato/versionamento observado e transição, não somente `task + destino`. Após voltar ao desenvolvimento, um novo ciclo de revisão continua possível. Fatos já consumidos não voltam a produzir movimento. O ciclo associado é o comprovado para o fato original; reprocessar entrega antiga não pode atribuir o ciclo atual àquele fato e produzir uma chave nova.
5. Eventos atrasados devem ser comparados ao estado/versionamento atual do PR e da task. Não ordenar somente pela chegada HTTP. Consultar o provedor quando houver dúvida e manter pendência se não for possível confirmar; uma solicitação antiga de mudanças não desfaz um merge já confirmado.
6. **Parâmetros propostos para G-SPEC:** reter metadados de entregas terminadas por 30 dias, expurgo diário, sem armazenar corpo bruto; nunca expurgar pendências não resolvidas automaticamente. Preservar permanentemente os fatos aplicados e o histórico funcional, separados da tabela operacional. Após expirar a entrega, um replay continua sem efeito pelo fato consumido e pela validação de estado. A política não expira `TaskEvent`, `StageHistory` nem `AuditLog`.

### RF-04 — Identificação determinística da task

1. Resolver sempre no projeto associado ao repositório, nesta ordem:
   1. Vínculo explícito previamente registrado por **`vincular_pr(id, url)`**.
   2. Branch de origem no padrão **`task/T-XXXX-slug`**, extraindo o ID completo; `XXXX` representa a sequência numérica do método, com pelo menos quatro dígitos.
   3. Marcador único **`[T-XXXX]`** no título do PR.
2. Um vínculo explícito válido prevalece mesmo se branch/título apontarem para outra task; registrar a divergência para conferência. Sem vínculo explícito, branch válida prevalece sobre título. Se a fonte prioritária trouxer ID inexistente, duplicado ou inacessível, registrar erro de vínculo; não mascarar o erro buscando uma task de menor prioridade ou de outro projeto.
3. URL do vínculo deve resolver provedor/instância/repositório permitido e número estável do PR. Não requisitar URL arbitrária fornecida pelo cliente. Um PR associa-se a uma task neste contrato; uma task pode ter vários PRs, inclusive em repositórios autorizados distintos do mesmo projeto.
4. Na fase 2a, o vínculo torna-se estruturado. Preservar comentário e campo **PR** da fase 1 como apresentação compatível; o formato atual é `[pr] <url> · <papel> · mcp`, com URL também no campo **PR**. O novo `vincular_pr` chama operação dedicada que grava vínculo, projeção do campo e comentário de forma transacional. Registros legados não ganham prioridade explícita só por texto de comentário: importar candidato do campo **PR**, validá-lo no repositório permitido e solicitar confirmação de vínculo, preservando o original e a autoria conhecida. URL inválida ou conflito fica pendente, sem inventar autor nem associação. Alteração genérica do campo **PR** não pode substituir vínculo estruturado sem passar pela mesma operação autorizada.
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
4. Respeitar orçamento compartilhado da instalação GitHub App, cabeçalhos `X-RateLimit-*`, `Retry-After` e limites secundários; não assumir cota fixa, pois depende da instalação/plano. Diante de 403 por limite ou 429, aguardar a indicação do provedor, aplicar recuo com dispersão e não tentar outro token para contornar a restrição. Ver [limites oficiais do GitHub](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).
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
2. Reutilizar mecanismos de histórico/auditoria existentes, append-only e na mesma transação do movimento (D34/D47). Ator sistema identifica a integração, preservando separadamente o autor Git observado; não simular autorização humana com a identidade do autor do PR. A projeção confiável usa acréscimos no **JSON de `TaskEvent.Payload` existente**: `principalId`, `methodRole`, `authenticationKind`, `channel`, `correlationId`, `cycleId`, `stageHistoryId` e referências do fato/entrega quando existirem. `StageHistory.ActorId/ActorName` preservam a autoria real e o payload correlaciona a permanência; `AuditLog` usa contexto confiável e origem `webhook` no worker. Não são necessárias colunas novas no histórico para este desenho, nem reescrita de JSON antigo. Consultas tratam campos aditivos ausentes em eventos legados sem inventar principal/canal. Teste de worker comprova `AuditLog`, `StageHistory` e `TaskEvent` correlacionados e atômicos, e o rollback de um efeito não deixa nenhum deles isolado.
3. Tentativas recusadas, eventos ignorados e duplicados ficam em registro operacional/segurança com motivo; não criam um movimento fictício. Comentários padronizados de PR continuam separados do histórico automático.
4. Expor no projeto estado da integração, última reconciliação bem-sucedida, atraso, erros recuperáveis e PRs não vinculados. Segredos, tokens, corpo bruto e cabeçalhos de autenticação não aparecem em telemetria nem UI.

### RF-11 — Tokens com papel e proteção central, fase 2a

1. O admin do projeto emite tokens agente/sistema, consulta metadados e revoga tokens **pela UI de configurações do projeto**, após autenticação humana interativa. Token **humano só é emitido para a própria pessoa autenticada, por ela mesma**, que também precisa de `ManagePermissions` no projeto para essa operação; o admin pode consultar metadados/revogar, mas não emitir um segredo humano em nome de outro usuário. “Admin do projeto” é a capacidade efetiva de administrar permissões naquele projeto, usando D55 e o catálogo existente (`ManagePermissions` no escopo de projeto); não criar outra hierarquia de perfis. Qualquer token de método é recusado nos endpoints de emissão, mesmo se o papel for humano. A API aplica a mesma exigência, independentemente de a UI esconder a ação.
2. Cada token contém, no registro servidor, principal estável, papel **humano/agente/sistema**, organização e **um projeto**, permissões limitadas, emissor, emissão, validade obrigatória e revogação. O valor opaco aleatório tem pelo menos 256 bits, é mostrado uma única vez e armazenado apenas como hash; não é JWT de login nem GitHub installation token. Proposta operacional: validade padrão de 30 dias, máximo de 90 dias, sem refresh automático; limites sujeitos à aprovação PD-05.
3. Principal humano vincula-se obrigatoriamente ao próprio usuário que emite o token e não concede permissões que ele não possua; não existe parâmetro que permita escolher outro `HumanUserId`. Agente tem identidade própria estável e delegação limitada pelo admin emissor; sistema é vinculado à integração e só aplica fatos verificados. Rotacionar token do mesmo principal não muda a autoria de um bloqueio. Nenhum token de método emite outro token, se promove, recebe sessão interativa ou ganha autoridade administrativa pelo papel. Colocar token humano em `PRISMA_TOKEN` não permite conceder autorização de início nem liberar retomada.
4. A API verifica hash, expiração, revogação, tenant/projeto ativo, delegação e permissões em cada requisição, incluindo hub/rotas genéricas quando aplicáveis. Revogação vale na requisição seguinte sem depender de expirar um JWT/cache de longa duração. Não aceitar token em URL, não registrar Bearer e não mostrar o valor ao listar tokens. `LastUsedAtUtc` é informação operacional aproximada, atualizada no máximo uma vez por janela de cinco minutos por token; essa granularidade não guarda em cache nem posterga verificação de revogação/expiração.
5. Sessão humana da UI recebe contexto **`UiSession`** da autenticação humana existente validada; credencial opaca recebe contexto **`MethodToken`** e papel do registro servidor, inclusive se for humano. Worker recebe **`IntegrationWorker`** somente pelo escopo interno confiável. Nenhum cabeçalho `Origin`, body, `User-Agent` ou variável `PRISMA_ROLE` prova sessão interativa nem altera papel/canal. A sessão UI continua com suas permissões e regras atuais, sem restrições novas da matriz/anti-contorno. Conceder ou reconceder autorização humana de execução e registrar a liberação humana de retomada exige `UiSession`; nenhum `MethodToken` concede isso. A verificação é do tipo de credencial autenticada no servidor, não da declaração de que há uma pessoa no cliente.
6. Centralizar a matriz da Authorization Impact em **qualquer alteração de estado iniciada por principal agente/sistema**, inclusive drag-and-drop autenticado com esse token, move, update com `StageId`, lote, criação/duplicação/importação, troca de quadro/projeto, automação e efeitos indiretos de reclassificação/conclusão. Para esses principais também rejeitar remoção do ID do método, troca de quadro não mapeado, alteração de mapeamento/categoria e outras tentativas de contorno. Uma automação disparada pelo agente mantém o principal/papel/teto do agente até o último efeito, sem virar sistema/humano por executar internamente. Sessões humanas da UI **não recebem essas restrições novas**, inclusive para remover ID/trocar quadro: mantêm as permissões e validações atuais e, se invalidarem configuração do método, suspendem-se as promoções automáticas afetadas com motivo visível. Tokens humanos preservam permissões efetivas, mas continuam sem autoridade para conceder/liberar autorização humana interativa (item 5). Projetos sem método habilitado mantêm seu contrato existente para humanos; credenciais de método continuam presas ao próprio projeto e não ganham acesso externo.
7. O primeiro corte precisa provar que agente não inicia por nenhum desses caminhos. Criação/importação pelo agente segue o contrato da fase 1: Backlog ou registro concluído de atalho/incidente já realizado, nunca criação diretamente autorizada em desenvolvimento. Origem desconhecida/sem principal confiável não ganha a autoridade do sistema. Para o papel sistema, POST genérico pedindo movimento sem fato Git validado é recusado.
8. A retomada pelo agente exige principal do bloqueio, origem em execução autorizada, ciclo atual e **liberação por sessão humana interativa `UiSession`** registrada no backend; nenhum token de método, nem de papel humano, libera esse bloqueio. Comentário com marcador forjado não é prova. O registro de autorização e de bloqueio fica vinculado ao histórico permanente e a liberação referencia o bloqueio exato; outra movimentação/bloqueio invalida a liberação anterior. A pessoa na UI pode retomar diretamente segundo suas permissões existentes, sem receber a matriz do agente.
9. Expor consulta de identidade/regras efetivas do token, para o adaptador obter papel, principal, projeto, capacidades e expiração sem decodificar JWT. Respostas de autenticação inválida/expirada/revogada são 401; operação fora da matriz/escopo é 403; estado concorrente, 409, sempre em pt-BR e sem segredo.
10. **Mapeamento obrigatório para os serviços existentes:** `IMethodAccessContext` é scoped e obtido somente da autenticação/worker. Para agente/sistema, `ClaimTypes.NameIdentifier`, `CreatedBy`, `Comment.UserId` e `StageHistory.ActorId` usam a cadeia estável `method:<PrincipalId em formato D>`, nunca o usuário emissor e nunca o TokenId. `ClaimTypes.Name`/snapshot usam `MethodPrincipals.DisplayName`; não emitir e-mail de humano como se fosse do principal. Não criar um usuário Identity nem uma associação de organização/projeto artificial. `ActorPrincipalId` guarda o GUID nas novas tabelas, enquanto os campos string existentes recebem a representação compatível. Para sessão UI e token humano, autoria é o próprio Identity UserId, com principal do token preservado separadamente no payload quando houver.
11. `ProjectAccessService` e `PermissionService` reconhecem a identidade autenticada do método antes da consulta de membros Identity. Agente tem **teto de `ProjectRole.Member`**, limitado ao projeto do token; sistema recebe somente as capacidades da integração para leitura/vínculos e movimento com fato verificado. A permissão efetiva do agente é a interseção de `PermissionCeilingJson` explícito, catálogo de `Member` e permissões atuais do delegante naquele projeto; negação explícita prevalece. `AdministerOrganization`, `ManageMembers` e `ManagePermissions` nunca são herdadas do emissor. Delegante sem acesso/ativo ou delegação inválida suspende a credencial. Token humano cruza teto explícito com permissões atuais do próprio usuário e não personifica outro. Escopos de tarefa/quadro são resolvidos para o projeto antes de autorizar; outro projeto retorna 403/404 conforme o contrato, sem exposição. Não usar o GUID não humano como se fosse membro Identity nem chamar os serviços com o UserId do admin para emprestar sua autoridade.
12. O middleware de organização aceita apenas o tenant já validado pelo contexto de método e recusa divergência de cabeçalho; a busca de membro humano existente segue inalterada para `UiSession`. O worker estabelece seu contexto antes do acesso à política. `IMethodMoveService` só aceita fato verificado de contexto interno; um POST com token sistema não fabrica esse contexto por campos do payload. Os testes de F0/A/F1 precisam provar esse contrato com os serviços reais, não só com mocks de permissões.

### RF-12 — Migração do adaptador MCP na 2a

1. Admin configura projeto/quadro/campos/mapeamentos e emite token de agente na UI; operador injeta o valor em `PRISMA_TOKEN` por variável externa. `PRISMA_URL` e IDs de projeto/quadro são conferidos com o escopo retornado pela API. O processo do agente deixa de receber **email/senha**; retirar fallback de `PRISMA_EMAIL`/`PRISMA_PASSWORD` e o relogin após 401 no modo método.
2. `tools/prisma-mcp/src/config.ts` exige token; `api.ts` trata credencial como opaca, consulta identidade em vez de decodificar JWT em `userId`, elimina login/renovação com senha e mascara o token em erros. 401/403 não disparam tentativa com credencial humana: o adaptador manda **parar e avisar**. Renovação é nova emissão/rotação pelo admin e troca da variável externa.
3. `rules.ts` continua como redundância para mensagem rápida, alimentada pelas regras/papel efetivos da API. `PRISMA_ROLE`, se mantido temporariamente para compatibilidade, serve só para comparar expectativa e falhar em divergência; nunca autoriza. `regras_do_projeto` consulta o contrato do backend, com capacidades do corte habilitado.
4. `tasks.ts` usa operações autenticadas para mover, vincular PR e retomar bloqueio. `server.ts` passa a aguardar regras/identidade remotas. Os oito nomes de ferramentas permanecem compatíveis; não transferir estado de segurança para comentários. `setup.ts` em leituras apenas resolve estrutura existente; provisionamento passa a ação explícita do admin, sem criar/ajustar projeto e colunas por token de agente ao ler.
5. `import.ts` usa o mesmo token e validação da API; importação nunca restaura um status autorizado que o papel não pode conceder. Preservar tarefas de topo, **ID do metodo**, **HU** e tag. Importação de vínculos antigos segue RF-04; configuração errada não dispara mudança automática de dados da demonstração.
6. Atualizar README/DEMO e testes do pacote para token opaco, revogação, escopo, inexistência de fallback e API recusando até quando o guard local é removido. Transição de instalação é coordenada: backend 2a validado → emissão UI → troca da configuração MCP → revogação das credenciais antigas do método quando identificáveis, sem revogar contas/sessões humanas usadas em outros contextos.

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
- História canônica: [US-METODO-GIT-001](../stories/US-METODO-GIT-001.md), registrada pelo Maestro a partir do pedido do PO (D100, D101), integrada pelo PR #23.

## Acceptance Criteria

Os critérios pertencem ao corte indicado. Aceitar 2a não exige reconciliação nem gate/deploy; nenhuma fase fica implicitamente aprovada ao aceitar outra. Todas usam tasks de topo, campo **ID do metodo**, campo **HU** e tag, sem work item para a HU.

### Entrega 2a — API e webhook

1. **CA-2A-01 — Given** sessão humana interativa de admin do projeto, **When** emite pela UI token agente/sistema ou humano para si, **Then** define principal, projeto, papel e validade; o segredo aparece uma vez, metadados não o expõem, token humano para outro UserId é recusado e token de método de qualquer papel não emite tokens. Quem não administra o projeto não emite/revoga.
2. **CA-2A-02 — Given** token expirado/revogado ou de outro projeto/tenant, **When** usa API, UI de teste com esse Bearer, rotas genéricas ou MCP, **Then** acesso é negado sem efeitos; revogação vale na requisição seguinte e alterar papel/canal no cliente não eleva autoridade. Token sistema válido também não move por POST genérico sem fato verificado.
3. **CA-2A-03 — Given** task em Backlog/Pronta e token de agente, **When** tenta iniciar por MCP, move, update/StageId, lote, criação/importação, duplicação, troca de quadro, remoção de ID, automação ou outro efeito indireto do inventário F0, **Then** a API recusa independentemente do guard local e preserva o principal iniciador até o efeito final. Token humano colocado no MCP ou na chamada REST também recebe 403 ao tentar conceder autorização de início/liberação de retomada, sem ganhar sessão UI. **Regressão humana:** usuário humano comum com suas permissões atuais move pela UI/API de sua sessão em projeto com método e sem método, incluindo Backlog → Em desenvolvimento e transferência de quadro; não surge restrição nova de transição/anti-contorno para essa sessão.
4. **CA-2A-04 — Given** último bloqueio de execução autorizada, **When** o mesmo principal de agente retoma após liberação de sessão humana interativa para aquele bloqueio/ciclo, **Then** a API aceita mesmo após rotação do token; bloqueio alheio, antigo, originado em Backlog ou marcador forjado é recusado. Token humano/sistema/agente não registra a liberação; emissão/rotação não altera a autoria nem reutiliza aprovação de outro bloqueio.
5. **CA-2A-05 — Given** cadastro de repositório pelo admin, **When** valida GitHub App/instalação e Repository ID, **Then** somente o projeto permitido recebe fatos e a App não tem escrita de checks/status antes da fase 3; segredo/chave ficam externos.
6. **CA-2A-06 — Given** webhook GitHub, **When** verifica os bytes originais, **Then** assinatura correta permite entrada durável; corpo alterado, segredo errado, cabeçalho malformado, repositório divergente ou tamanho excedido não produz efeito.
7. **CA-2A-07 — Given** reenvio sequencial/concorrente, **When** usa mesmo delivery ID e corpo, **Then** há no máximo um movimento/histórico; mesmo ID com hash divergente dá conflito, e queda antes/depois do commit não perde nem duplica o efeito.
8. **CA-2A-08 — Given** vínculo explícito T-0001, branch T-0002 e título T-0003, **When** identifica PR, **Then** vence vínculo explícito; na sua ausência vence branch e, sem ambos, título; fonte prioritária inválida/ambígua não movimenta task de fallback/outro projeto.
9. **CA-2A-09 — Given** task identificada, **When** cria branch padrão ou abre PR draft, **Then** só registra vínculo/observação; branch fora do padrão não cria vínculo inferido.
10. **CA-2A-10 — Given** task autorizada em desenvolvimento, **When** abre PR não draft ou o torna pronto, **Then** move uma vez para revisão; uma task em Backlog/Pronta/Bloqueada não é autorizada pelo PR.
11. **CA-2A-11 — Given** task em revisão, **When** fecha PR sem merge ou confirma changes requested vigente, **Then** volta ao desenvolvimento; fechamento com merge e review antiga não acionam essa volta.
12. **CA-2A-12 — Given** task em revisão, **When** mergeia na principal com leitura completa de todos os PRs associados/identificáveis, **Then** vai para teste apenas sem outro PR aberto, incluindo drafts e outros repositórios do projeto; consulta incompleta não promove.
13. **CA-2A-13 — Given** fato antigo, origem incompatível ou intervenção humana posterior, **When** processa webhook, **Then** não há regressão/salto forçado; concorrência e novo ciclo legítimo são distinguidos pela versão/fato consumido.
14. **CA-2A-14 — Given** repositório GitHub do DETRAN com título livre e documentos fora do Git, **When** existe vínculo explícito ou branch padrão, **Then** resolve a task sem ler documentos nem exigir ID no título.
15. **CA-2A-15 — Given** PR sem vínculo explícito, sem branch no padrão e sem marcador identificável no título, **When** é observado, **Then** aparece na lista do projeto como não vinculado sem movimento; pessoa autorizada vincula pela UI com teclado/mobile e o estado atual é reavaliado, respeitando escopo e prioridade.
16. **CA-2A-16 — Given** transição por ui/mcp/webhook, **When** consulta histórico autorizado, **Then** encontra antes/depois, principal/papel, tipo de autenticação, canal confiável e correlação, sem segredos. O teste de worker real comprova tenant da integração e o conjunto atômico `AuditLog` com origem webhook, `StageHistory` com autoria sistema e `TaskEvent.Payload` com canal/correlação e `stageHistoryId`; não usa identidade do emissor nem do autor Git como autorização humana. Recusas não criam movimento fictício, expurgo de entrega não apaga prova permanente nem permite replay.
17. **CA-2A-17 — Given** MCP configurado com token opaco emitido pela API, **When** lê/escreve ou recebe 401/403, **Then** usa identidade/regras do backend, não decodifica JWT nem faz login por email/senha, não contorna a API e manda parar e avisar no erro; ler não provisiona estrutura.
18. **CA-2A-18 — Given** campo PR legado ou comentário padronizado, **When** migra vínculo, **Then** preserva original/autoria conhecida, valida repositório e requer confirmação antes de prioridade explícita; vínculo novo grava tabela/campo/comentário atomicamente, sem trocar tasks de topo por subitens.

### Entrega 2b — Reconciliação

1. **CA-2B-01 — Given** webhook perdido e PR atualizado dentro da janela, **When** executa rodada periódica, **Then** recupera fatos comprovados em ordem causal, com um único efeito mesmo concorrendo com webhook.
2. **CA-2B-02 — Given** PR aberto conhecido/pendência fora da janela ou indisponibilidade prolongada, **When** reconcilia, **Then** revisita os conhecidos e sinaliza a necessidade de recuperação histórica controlada sem declarar sincronização completa indevidamente.
3. **CA-2B-03 — Given** paginação, 403 por limite/429/5xx ou interrupção, **When** retoma a rodada, **Then** preserva cursor, respeita orçamento/Retry-After e não promove tarefa com visão parcial dos PRs.
4. **CA-2B-04 — Given** fato já aplicado, entrega expirada ou intervenção humana mais recente, **When** o polling observa novamente, **Then** não duplica/regrede status; efeito novo registra canal reconciliacao, autor sistema e rodada, exibindo atraso/erros ao projeto.

### Entrega 3 — Gate e deploy

1. **CA-3-01 — Given** autorização humana e política de fase 3 aprovadas, **When** avalia HEAD do PR, **Then** publica prisma/autorizacao para aquele SHA/versão, sem sucesso para task não vinculada, bloqueada ou negada; novo SHA exige nova avaliação.
2. **CA-3-02 — Given** principal protegida em repositório descartável, **When** falta sucesso da GitHub App confiável ou agente tenta forjar gate, **Then** merge permanece impedido; Prisma não faz merge e o teste não altera repositório de produção.
3. **CA-3-03 — Given** revogação, falha de publicação ou resultado atrasado, **When** publica/reprocessa, **Then** não libera por decisão obsoleta, preserva pendência e evidencia indisponibilidade; não presume revogação instantânea de sucesso já publicado durante queda.
4. **CA-3-04 — Given** task em teste e revisão implantada comprovadamente cobrindo seus merges, **When** recebe deploy bem-sucedido no ambiente configurado, **Then** conclui uma vez; preview, falha, ambiente/revisão errados não concluem nem dispensam homologação exigida.

## Data Impact

### Proposta física da 2a para G-MIGRATION

Nenhuma tabela/migration é criada nesta tarefa. Nomes e tipos abaixo são a proposta concreta a aprovar. Tipos SQL Server: GUID = `uniqueidentifier`; UTC = `datetimeoffset(7)`; versão concorrente = `rowversion`; campos opcionais estão marcados `NULL`. Nas tabelas com Id, a PK é `Id uniqueidentifier`, salvo indicação diferente. `OrganizationId`/`ProjectId` são obrigatórios onde listados. IDs de ator humano usam `nvarchar(450)` compatível com Identity; principal do método usa GUID estável separado do token.

| Tabela proposta | Colunas além da PK | Índices/restrições |
|---|---|---|
| `MethodProjectSettings` | `ProjectId` (PK/FK), `OrganizationId`, `BoardId`, `MethodIdFieldId`, `HuFieldId`, `PrFieldId` (GUIDs), `Enabled bit`, `GitPhase tinyint` (0 = sem Git, 1 = 2a, 2 = 2b, 3 = fase 3), `AutonomyLevel tinyint`, `PolicyVersion int`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion` | UNIQUE BoardId no primeiro corte; índice OrganizationId/Enabled; FKs projeto/quadro/campos existentes, validar que pertencem ao projeto; não habilitar se definições forem ausentes/duplicadas |
| `MethodStageMappings` | `ProjectId` e `StatusCode varchar(32)` (PK composta), `StageId`, `CreatedAtUtc` | UNIQUE ProjectId/StageId; FK settings e Stage; CHECK códigos fixos dos sete status; nomes visíveis não são identidade |
| `MethodPrincipals` | `OrganizationId`, `ProjectId`, `Role tinyint`, `DisplayName nvarchar(200)`, `HumanUserId nvarchar(450) NULL`, `DelegatedByUserId nvarchar(450)`, `PermissionCeilingJson nvarchar(3000)`, `IsActive bit`, `CreatedAtUtc`, `RowVersion` | Índice ProjectId/Role/IsActive; CHECK papéis humano/agente/sistema; humano exige usuário; teto de permissões validado no catálogo; FK usuário opcional sem cascade; identidade não muda na rotação de token |
| `MethodApiTokens` | `PrincipalId`, `ProjectId`, `Name nvarchar(120)`, `TokenHash binary(32)`, `TokenPrefix varchar(24)`, `IssuedByUserId nvarchar(450)`, `IssuedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc NULL`, `RevokedByUserId nvarchar(450) NULL`, `LastUsedAtUtc NULL` (granularidade 5 min), `RowVersion` | UNIQUE TokenHash; índice ProjectId/PrincipalId/RevokedAtUtc/ExpiresAtUtc; CHECK ExpiresAtUtc > IssuedAtUtc; FK principal/projeto; nunca persistir segredo bruto; prefixo não autentica |
| `MethodTaskStates` | `WorkItemId` (PK/FK), `OrganizationId`, `ProjectId`, `MethodTaskId varchar(32)`, `CycleId`, `ActiveAuthorizationEventId GUID NULL`, `BlockingEventId GUID NULL`, `BlockingPrincipalId GUID NULL`, `ResumeApprovalEventId GUID NULL`, `LastObservedFactId GUID NULL`, `RowVersion` | UNIQUE ProjectId/MethodTaskId; índice ProjectId/WorkItemId; FKs para eventos/principal; task existente deve ser de topo; linha guarda controle de autorização/concorrência, **não outro status** |
| `MethodAuthorizationEvents` | `OrganizationId`, `ProjectId`, `WorkItemId`, `CycleId`, `EventKind varchar(24)` (Granted/Revoked/Blocked/ResumeAllowed/Resumed), `RelatedEventId GUID NULL`, `ActorPrincipalId GUID NULL`, `ActorUserId nvarchar(450) NULL`, `ActorName nvarchar(200)`, `TaskEventId`, `Reason nvarchar(1000)`, `CreatedAtUtc` | Índice ProjectId/WorkItemId/CreatedAtUtc/Id; UNIQUE TaskEventId; append-only, sem apagar/sobrescrever; FK relacionada restrita à mesma task/ciclo; evento resume precisa referenciar bloqueio e resposta humana |
| `GitRepositoryIntegrations` | `OrganizationId`, `ProjectId`, `Provider varchar(16)`, `Host nvarchar(255)`, `RepositoryId bigint`, `RepositoryFullName nvarchar(300)`, `DefaultBranch nvarchar(255)`, `AppId bigint`, `InstallationId bigint`, `AppPrivateKeySecretRef nvarchar(512)`, `WebhookSecretRef nvarchar(512)`, `PreviousWebhookSecretRef nvarchar(512) NULL`, `PreviousSecretValidUntilUtc NULL`, `SystemPrincipalId`, `Enabled bit`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion` | UNIQUE Provider/Host/RepositoryId (um projeto destinatário no primeiro ciclo); índice ProjectId/Enabled; FK settings e principal sistema; valores são referências externas, não chaves/tokens; validar instalação/repositório com a App |
| `GitPullRequestObservations` | `IntegrationId`, `ProjectId`, `PullRequestNumber int`, `Url nvarchar(1024)`, `Title nvarchar(1000)`, `HeadBranch nvarchar(255)`, `BaseBranch nvarchar(255)`, `HeadSha varchar(64)`, `MergeSha varchar(64) NULL`, `State varchar(16)`, `IsDraft bit`, `ProviderUpdatedAtUtc`, `ObservedAtUtc`, `UnlinkedReason nvarchar(500) NULL`, `RowVersion` | UNIQUE IntegrationId/PullRequestNumber; índice ProjectId/State/ProviderUpdatedAtUtc; identidade de task é resolvida no vínculo, sem segunda coluna de status da task |
| `TaskGitLinks` | `ProjectId`, `IntegrationId`, `WorkItemId`, `PullRequestObservationId GUID NULL`, `BranchName nvarchar(255) NULL`, `Source varchar(16)` (explicit/branch/title), `CreatedByPrincipalId GUID NULL`, `CreatedByUserId nvarchar(450) NULL`, `CreatedAtUtc`, `RowVersion` | UNIQUE filtrado PullRequestObservationId quando não nulo (um PR/uma task); UNIQUE filtrado IntegrationId/BranchName quando PR nulo; índice ProjectId/WorkItemId; CHECK PR ou branch presente; task pode ter vários links; origem explícita só por operação autenticada |
| `GitWebhookDeliveries` | `IntegrationId`, `ProjectId`, `Provider varchar(16)`, `DeliveryId varchar(100)`, `BodyHash binary(32)`, `EventName varchar(64)`, `EventAction varchar(64) NULL`, `NormalizedEventJson nvarchar(max)`, `ReceivedAtUtc`, `Status varchar(24)`, `Attempts int`, `NextAttemptAtUtc NULL`, `LeaseUntilUtc NULL`, `ProcessedAtUtc NULL`, `LastErrorCode varchar(64) NULL`, `CorrelationId varchar(64)`, `RowVersion` | UNIQUE IntegrationId/Provider/DeliveryId; índice Status/NextAttemptAtUtc/LeaseUntilUtc; índice ProcessedAtUtc; CHECK tentativas não negativas; JSON mínimo validado/sem segredos para retry, **não corpo bruto**, expurgo operacional de 30 dias proposto |
| `GitAppliedFacts` | `ProjectId`, `IntegrationId`, `WorkItemId`, `CycleId`, `SemanticKey binary(32)`, `FactType varchar(40)`, `ProviderEntityId nvarchar(128)`, `ProviderRevision nvarchar(128)`, `FromStageId`, `ToStageId`, `TaskEventId`, `DeliveryId GUID NULL`, `AppliedAtUtc` | UNIQUE ProjectId/WorkItemId/SemanticKey; UNIQUE TaskEventId; índice IntegrationId/ProviderEntityId; retenção permanente; SemanticKey canoniza PR/review/fato/ciclo, não só destino; DeliveryId é correlação lógica preservada mesmo após expurgo, **sem FK que bloqueie o expurgo** |

Regras comuns: `ProjectId` referencia Projects e a organização é revalidada; relações entre novas tabelas devem usar chaves alternativas compostas por ProjectId/Id para impedir vínculos cruzados no banco, além da verificação da aplicação. FKs usam NoAction/Restrict para não apagar configuração ou evidência em cascade. Arquivamento preserva os registros e torna operações indisponíveis conforme as regras do projeto; token/principal é revogado/desativado, não apagado. Lease tem vencimento e compare-and-swap/rowversion; worker caído pode ser retomado. A transação do efeito inclui histórico existente, fato consumido e estado de entrega, com revalidação da origem canônica no WorkItem.

O campo **ID do metodo** (sem acento) continua em `ProjectCustomFields`/`WorkItemCustomFieldValues` existentes; `MethodTaskStates.MethodTaskId` é índice de identidade, não uma segunda autoridade. Qualquer criação/update/importação do valor deve sincronizar o registro na mesma transação e rejeitar duplicata. Candidatos legados duplicados não são renumerados nem apagados. Tasks continuam com `ParentId` nulo; **HU** e tag homônima são mantidas nas estruturas existentes. Campo **PR** é projeção compatível de vínculos; não cria hierarquia.

### Migration prevista e ensaio

Propor uma migration aditiva **`Add_MethodGit_Phase2a`** em `src/Prisma.Workspace.Infrastructure/Persistence/Migrations/`, com Designer e atualização do ModelSnapshot gerados uma única vez pelo integrador **após G-MIGRATION**. Criar as onze tabelas acima, constraints e índices; manter nomes/tipos Identity e tabelas existentes. Não adicionar status duplicado a WorkItems, não alterar ParentId, não criar work item HU e não criar tabelas de reconciliação/gate/deploy da fase seguinte.

O script de backfill, se aprovado junto do gate, habilita apenas projetos explicitamente selecionados: verifica campos, unicidade do ID do método, quadro/colunas e tasks de topo; preenche índice de identidade e mapeamento validado, **sem fabricar autorização humana, principal ou vínculo explícito retroativo**. Ambiguidades abortam a habilitação daquele projeto com relatório sem segredos. Configuração de segredos e GitHub App é externa e posterior à migration. Nenhum projeto é automaticamente convertido em projeto do método.

Ensaio exigido para o gate: gerar script SQL revisável, aplicar em banco descartável limpo e cópia restaurada autorizada, comparar contagens/ParentId/status/histórico antes/depois, conferir restrições e corrida de índice único/delivery, retry após queda e expurgo sem perda de fatos. Não executar esse ensaio no prisma-dev reservado para demonstração. **Down é funcional:** remove as onze tabelas novas em ordem inversa das dependências de FK, sem remover/renomear tabela ou coluna legada. Ordem de referência: GitAppliedFacts, GitWebhookDeliveries, TaskGitLinks, GitPullRequestObservations, GitRepositoryIntegrations, MethodTaskStates, MethodAuthorizationEvents, MethodApiTokens, MethodPrincipals, MethodStageMappings, MethodProjectSettings; a geração confirma a ordem real das FKs. `LastObservedFactId` é correlação lógica, sem FK que crie ciclo com fatos. Testar Up → Down → Up em banco descartável e verificar que tabelas/linhas legadas de tarefas, ParentId, colunas e histórico permanecem. **Rollback operacional em ambiente com dados prefere desabilitar a integração e preservar tabelas**; executar o Down destrutivo em ambiente institucional exigiria operação humana autorizada e backup. O ensaio descartável prova a reversibilidade técnica e não concede permissão de apagar fatos/auditoria em produção.

Auditoria existente recebe somente campos aditivos no JSON `TaskEvent.Payload`, incluindo a referência `stageHistoryId`, conforme RF-10.2; preserva estrutura e registros de `TaskEvent`, `StageHistory` e `AuditLog`, sem colunas novas de histórico neste desenho e sem reescrever eventos antigos. A tabela append-only de autorização e quaisquer mudanças de estrutura do histórico também precisam de G-HISTORY conforme AGENTS. Esta revisão só descreve o contrato para esses gates.

### Evoluções posteriores, fora da migration 2a

- **2b:** `GitReconciliationRuns`/cursor por integração, janela, orçamento, erro e continuidade; reutilizar fatos/deliveries da 2a, com canal reconciliacao.
- **3:** `GitStatusPublications`/saída durável por repositório/PR/SHA/versão da autorização, retentativas e evidência de deployment; detalhes de ambientes e política dependem de PD-04/07.

## Authorization Impact

Papel do método (`humano`, `agente`, `sistema`) é distinto do perfil funcional Prisma. A permissão efetiva é a interseção entre papel, projeto, permissões existentes, estado e autonomia configurada. Visualizador não vira escritor por possuir token humano (D17/D18/D55/D58).

| Operação/transição | Sessão humana interativa da UI | Agente | Sistema de integração |
|---|---|---|---|
| Ler regras/tasks no escopo | Permissões existentes | Sim, pelo teto efetivo | Somente o necessário à integração |
| Criar task, comentar e vincular PR | Permissões existentes, sem matriz nova | Contrato fase 1 e teto, sem autorização implícita | Observações/vínculos inferidos; não criar tasks |
| Backlog → Pronta e Backlog/Pronta → Em desenvolvimento | Movimenta como hoje; a sessão concede autorização humana quando aplicável | Nunca | Nunca; branch/PR não autoriza |
| Qualquer status exceto Concluída → Bloqueada | Movimenta como hoje | Sim, com motivo/autoria; retomada exige origem autorizada | Não por evento Git |
| Bloqueada → Em desenvolvimento | Movimenta como hoje; pode também liberar retomada do agente por ação interativa separada | Só bloqueio próprio/autorizado e liberação interativa do ciclo | Não |
| Em desenvolvimento → Em revisão | Movimenta como hoje | Apenas nível 0 | PR não draft/pronto confirmado |
| Em revisão → Em desenvolvimento | Movimenta como hoje | Não como compensação de Git | Fechado sem merge ou changes requested vigente |
| Em revisão → Em teste | Movimenta como hoje | Não para reproduzir merge | Merge na principal, múltiplos PRs RF-05 |
| Em teste → Concluída | Movimenta como hoje, sem nova exigência de homologação na UI | Não para reproduzir deploy | Somente fase 3, fora da 2a |
| Demais transições manuais/troca de quadro/remoção de ID | Permissões e validações existentes, em todos os projetos; promoções afetadas podem ficar suspensas sem bloquear a pessoa | Não; regras anti-contorno aplicáveis | Não; só fatos expressamente previstos |
| Configurar integração/política ou emitir token | Sessão humana admin no projeto; token humano só para si | Não | Não |

**Token de papel humano:** usa as permissões atuais do próprio usuário e seu teto, mas continua uma credencial `MethodToken`, sem personificação nem sessão interativa. Não concede autorização humana de início/reinício ou liberação de retomada. A API recusa qualquer operação cujo efeito exigiria fabricar essa concessão, mesmo por caminho indireto ou `PRISMA_ROLE=humano`. As demais operações não recebem a matriz restritiva do agente; não há poder administrativo emprestado. Sessão humana UI não é reconhecida por `Origin` nem por declaração do cliente, conforme RF-11.5.

O contrato específico `prisma.md` prevalece para transições comprovadas por Git: o agente comenta e aguarda; não compensa webhook perdido movendo status. A permissão genérica de nível 2 de `fluxo.md` não libera esse atalho em modo Prisma. A reconciliação entre conclusão sem homologação do fluxo genérico e a tabela específica merge → teste → deploy → concluída está em PD-04.

**Requisito da API:** hoje a restrição de início está apenas no adaptador. A API deve obter papel e tipo de autenticação da credencial/contexto servidor e negar falsificação de papel/canal. A matriz e as regras anti-contorno valem para principais agente/sistema em qualquer escrita, inclusive endpoints genéricos, lote e caminhos indiretos. Sessões humanas UI mantêm as regras atuais, sem nova matriz; tokens humanos não fabricam autorização interativa. Não basta proteger `mover_status` no MCP. Retomada de bloqueio não pode permitir Backlog → Bloqueada → Em desenvolvimento para iniciar sem autorização.

Tokens humano/agente/sistema têm escopo de projeto, validade e revogação; emissão pela UI de admin do projeto e checagem na API conforme RF-11. Agente não emite tokens. Sistema usa identidade dedicada restrita à integração e fatos verificados, sem login de pessoa emprestado. A credencial Git é GitHub App (D101), com leitura dos PRs/reviews/deployments e, somente na fase 3, escrita de status ou checks; sem escrita de conteúdo, merge ou administração de proteção. **PD-02 e PD-03 resolvidas pela D101 e pelo detalhamento de emissão desta tarefa**: o MCP do método escreve diretamente conforme papel, enquanto o assistente interno mantém as sugestões/aprovações da D91. Não existe gate pendente para rediscutir essa separação; continuam pendentes as aprovações desta spec e da migration concreta.

## Contracts

Contratos externos propostos, a detalhar em DTOs somente após aprovação:

| Entrada/saída | Contrato |
|---|---|
| Webhook GitHub | `POST /api/integrations/git/github/{integrationId}/webhook`; corpo original; `X-Hub-Signature-256`, `X-GitHub-Delivery`, `X-GitHub-Event`; validação RF-02 |
| Tokens, fase 2a | Propor `POST/GET /api/projects/{projectId}/method/tokens` para emissão/metadados, e `POST /api/projects/{projectId}/method/tokens/{tokenId}/revoke`; somente sessão humana interativa admin; humano emitido só para o próprio usuário, sem campo de destinatário; segredo só na resposta inicial |
| Autorização interativa/retomada | A sessão UI existente concede autorização ao mover segundo as regras atuais. Propor `POST /api/projects/{projectId}/method/tasks/{workItemId}/resume-approval` com bloqueio/ciclo e motivo para liberar retomada; somente `UiSession` com permissão atual de mudança de status, 403 para qualquer token do método. A liberação não move a task por si só |
| Identidade/regras, fase 2a | Propor `GET /api/projects/{projectId}/method/me` e `/rules`; papel, principal, escopo, validade, matriz/capacidades efetivas, sem segredo; não aceitar projeto fora do token |
| Cadastro de repositório, fase 2a | Propor `POST/GET/PUT /api/projects/{projectId}/method/repositories`; admin humano; IDs GitHub App/instalação/repositório e referências a segredos, nunca valores secretos em URLs ou logs |
| Operações da fase 1 | Preservar `regras_do_projeto`, `obter_task`, `listar_tasks`, `proximas_tasks`, `criar_task`, `mover_status`, `comentar` e `vincular_pr`; API passa a ser autoridade de permissão |
| `regras_do_projeto()` | Deve expor matriz efetiva, autonomia, convenções, mapeamento de status e capacidades habilitadas, sem segredos |
| Vínculo explícito | ID do método no projeto e URL canônica do PR; resposta com task interna, PR e origem do vínculo; reenvio idêntico é idempotente; vínculo conflitante exige resolução autorizada |
| PRs não vinculados | Propor `GET /api/projects/{projectId}/method/pull-requests?linked=false` paginado e `POST /api/projects/{projectId}/method/pull-requests/{observationId}/link` com work item/ID do método e motivo; admin/membro com permissão efetiva de vínculo; idempotência e autoria confiáveis |
| Publicação GitHub, fase 3 | Commit Status no SHA com contexto `prisma/autorizacao`, estado, descrição curta sem segredos e URL de detalhes protegida; ou Check equivalente conforme PD-04 |

**PD-08 resolvida (D101):** GitLab fica fora do primeiro ciclo. Preservar interface de provedor que autentica/normaliza entregas, consulta PRs/reviews/deployments e publica gate, com eventos de domínio independentes de cabeçalhos GitHub. A única implementação habilitada neste ciclo é GitHub. Um futuro adaptador GitLab precisa de spec/revisão própria para autenticação, idempotência e equivalência de reviews/deploy; não criar endpoint GitLab nem alegar paridade entregue.

Todos os eventos são normalizados com provedor, integração, ID do repositório, tipo do fato, PR/branch/deploy, revisão/instante do provedor e correlação. Tokens de método e tokens GitHub App são credenciais diferentes e não são intercambiáveis.

## Dependencies

- Fase 1 do Maestro presente nesta base: operações do método e formato legado do vínculo explícito; a 2a migra o adaptador conforme RF-12. Nenhum código de `tools/prisma-mcp` é alterado nesta entrega documental.
- `AGENTS.md`; D17/D18/D34/D36/D47/D55/D58/D62/D65/D89/D91/D97/**D100/D101**; [permissões](user-access-permissions.md), [histórico](audit-leadtime-history.md) e [colunas independentes](independent-board-columns.md).
- API GitHub e GitHub App com privilégios mínimos; rotina agendada somente na 2b; saída persistente de gate somente na 3, sem escolher nova biblioteca nesta revisão.
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

### Decisões resolvidas — não reabrir

- **PD-02 — Direção resolvida pela D101 e tarefa 9:** tokens humano/agente/sistema emitidos pela API, com escopo de projeto, validade e revogação; emissão pela UI por admin do projeto, token humano apenas para si e autorização/liberação humana somente por sessão interativa conforme correção da revisão git-spec. O texto corrigido ainda passa por nova revisão antes da implementação. GitHub App é a credencial do provedor. Identidade servidor e migração MCP seguem RF-11/12; `vincular_pr` passa a operação estruturada, com preservação/validação do legado.
- **PD-03 — Resolvida pela D101:** MCP externo do método escreve diretamente o que a matriz do token permite; assistente IA interno continua propondo com aprovação humana (D91). Não exigir nova decisão sobre essa separação.
- **PD-08 — Resolvida pela D101:** GitHub, inclusive DETRAN, neste ciclo; GitLab fora, provedor plugável. Não há implementação GitLab a validar para aceitar a 2a.

### Registro humano pendente para liberar a 2a

O GOALS autoriza implementar/publicar o corte 2a e determina migration aditiva com Down funcional, humanos UI sem regressão e revisão prévia. Ele não registra expressamente a adoção dos valores/regras propostos abaixo. **O Maestro deve apontar a autorização humana anterior que os adota ou registrar BLOQUEIO para o PO; este documento não transforma recomendações em decisões por conta da Forja.** Registrar fonte/data, valores adotados ou ajuste e gates aplicáveis; atualizar o status para approved somente após essa autorização efetiva e a revisão corrigida. Até lá, F0 de produto/migration permanece bloqueado; preparação de ambiente e correção documental podem continuar.

Itens concretos a confirmar para a 2a: PD-01 (provisionamento/quadro/categorias, com nomes visíveis da D100), PD-05 apenas validade/retenção/rotação/corpo/retry do corte, e PD-06 apenas reabertura, reviews e merge retido. PD-04/07 e agendamento da 2b ficam fora dessa liberação. A direção já fechada da D101 não é reaberta.

### Recomendações concretas para decisão humana

| PD e impacto | Recomendação | Motivo |
|---|---|---|
| **PD-01 — aguardando aprovação do PO**; bloqueia fechamento do workflow 2a | Cadastrar a história proposta `US-METODO-GIT-001` (ID a validar no catálogo), usar um quadro **Método** por projeto com as sete colunas; somente Concluida tem categoria concluída, as demais abertas; tasks de topo, HU só em campo/tag. Não automatizar descendência nova nem conclusão da HU. Habilitar somente por provisionamento explícito do admin. | Mantém as tasks visíveis no Kanban e evita segunda fonte de status ou conclusão implícita de HU. |
| **PD-04 — aguardando aprovação do PO**; bloqueia política da fase 3 | Usar **GitHub Commit Status** `prisma/autorizacao` emitido pela GitHub App, com origem obrigatória na proteção da principal. Success exige task vinculada, autorização humana do ciclo vigente, estado Em desenvolvimento/Em revisao, ausência de bloqueio/revogação e SHA atual; não substitui CI nem reviews humanas exigidas. Preservar merge → Em teste e deploy → Concluida em todos os níveis, sem atalho de conclusão por agente; se há homologação, exigir aceite humano registrado antes de concluir por deploy. | Um contrato único por SHA evita autorizar via evento Git e mantém evidência humana separada da aprovação técnica. |
| **PD-05 — aguardando aprovação do PO**; retenção/tokens afetam 2a, agendamento afeta 2b | Adotar os padrões já propostos: **5 min**, janela **7 dias**, sobreposição **1 h**, páginas **100**, orçamento **200 leituras/rodada/repositório** subordinado à instalação, entregas terminadas **30 dias** com expurgo diário, fatos/histórico permanentes. Completar com token padrão **30 dias**, máximo **90**, uso operacional atualizado em janela de **5 min**, rotação HMAC com anterior por **24 h**, corpo até **5 MiB** e alerta após **15 min** sem rodada bem-sucedida. Recuperação além de 7 dias requer solicitação humana de janela ampliada. | Limita custo e exposição, preserva prova permanente e torna atraso/perda longa visíveis. O limite recomendado de 5 MiB reduz rejeição de PRs com descrição longa; medir payloads simulados Unicode/descritivos próximos ao limite e testar 413 sem efeito. Na 2a, sem reconciliação periódica, uma entrega rejeitada por tamanho exige aviso visível e reenvio/controlado pelo administrador, sem afirmar recuperação automática. |
| **PD-06 — aguardando aprovação do PO**; afeta transições 2a e recuperação 2b | Se um merge ficou retido por outro PR aberto e este fecha sem merge, aplicar a regra específica de retorno revisão → desenvolvimento e **não promover pelo merge antigo**; exigir decisão humana posterior de prontidão ou novo ciclo comprovado. PR reaberto não draft conta como nova abertura apenas quando a task está em desenvolvimento; retorno a draft só registra fato. Para reviews, considerar a última não dispensada de cada revisor no HEAD vigente; qualquer changes requested efetivo impede promoção. Vínculo tardio não desfaz intervenção humana posterior. | Evita que um merge antigo esconda trabalho recusado ou cancelado e impede regressão por eventos fora de ordem. |
| **PD-07 — aguardando aprovação do PO**; bloqueia conclusão automática da fase 3 | Observar **GitHub Deployment Status success** no ambiente literal **production** por integração; comprovar SHA implantado e todos os merges vinculados, incluindo squash/rebase, antes de Em teste → Concluida. Se há homologação, exigir aceite humano adicional. Preview/falha não concluem; rollback só registra ocorrência e alerta, sem reabrir automaticamente. Outros ambientes/fontes precisam de cadastro explícito e nova validação. | Deployment com revisão verificável demonstra o que chegou a produção sem confundir CI verde, preview ou rollback com aceite. |

Todas as tabelas/rotas e detalhes técnicos desta spec continuam propostas até G-SPEC/G-MIGRATION. Em particular, configurar webhook de repositório para preservar segredo por repositório e estender payload de histórico exigem validação nos gates; não são configuração operacional executada por esta revisão. D101 resolve a direção do produto, não aprova automaticamente cada índice ou política proposta. PD-04/07 não bloqueiam implementar 2a após aprovar as pendências que a afetam, e PD-05 pode ser aprovada por corte.

## Test Gate Mapping

Plano futuro; esta entrega executa somente verificações documentais e não acessa o prisma-dev ou seu banco.

| Corte/camada | Provas e suíte proposta | Critérios |
|---|---|---|
| 2a — xUnit autenticação/autorização + integração HTTP | Emissão UI/API restrita a sessão admin, humano só para si e token de método não emite/libera/autoriza, hash/revogação/expiração, token opaco/escopo, principal estável sem herdar admin, papel/canal não falsificáveis, regressão humana UI com/sem método, matriz agente/sistema nos caminhos nominais F0 e efeitos indiretos | CA-2A-01 a 04, 17 |
| 2a — xUnit webhook/provedor | GitHub App com permissões mínimas, assinatura original/Unicode/corpo alterado, segredo errado/rotacionado, evento não suportado, instalação fora do escopo e normalização sem depender de documentos | CA-2A-05, 06, 08 a 14 |
| 2a — integração SQL/concorrência | Dois POSTs iguais em paralelo, queda entre persistência e efeito, rowversion/lease, transação com histórico, fato consumido após expurgo, índices de identidade/vínculo e conflito de campos legados | CA-2A-07, 13, 16, 18 |
| 2a — Vitest UI e testes Node do MCP | Emissão única/metadados/revogação, teclado/mobile, listagem e vínculo; token opaco sem senha/refresh, papel do backend, guard local apenas redundante, setup de leitura não escreve | CA-2A-01, 02, 15, 17, 18 |
| 2a — Playwright desktop/mobile | Admin emite/revoga token na UI, agente é recusado por API direta e não consegue pela UI; task própria de topo com HU/tag; webhook assinado move após reload; PR Detran com título livre; não vinculado é associado pela UI; histórico e persistência | CA-2A-01 a 03, 08 a 18 |
| 2b — xUnit com relógio/provedor controlados + integração | Paginação, janela/cursor, PR fora da janela, fatos perdidos, corrida polling/webhook, 403/429/5xx, cota compartilhada, leitura parcial e canal de auditoria | CA-2B-01 a 04 |
| 2b — Playwright desktop/mobile | Webhook omitido de propósito, reconciliação recupera e UI exibe coluna/histórico/atraso; dados próprios, sem seed | CA-2B-01, 02, 04 |
| 3 — xUnit + integração de provedor | Decisão por SHA/versão, permissão de publicação somente após habilitar fase 3, novo commit, revogação/publicação atrasada e deployment/merge/squash/rebase/ambiente | CA-3-01, 03, 04 |
| 3 — Playwright + repositório GitHub descartável autorizado | Proteção exige origem da App e sucesso, usuário/agente não forja status; deploy observado conclui com prova, sem merge/deploy no repositório institucional | CA-3-01 a 04 |

Testes usam factories únicas, limpeza limitada às fixtures e sincronização por resposta/estado, sem sleeps. Tempo constante é verificado por uso da primitiva criptográfica e revisão de código, não por teste frágil de duração. Sem repositório descartável autorizado não declarar branch protection comprovada por mock. G-SPEC/G-MIGRATION e gates de workflow/histórico aplicáveis vêm antes de código; depois, build .NET/xUnit, `npx tsc -b`, lint/Vitest/build, testes do pacote MCP e E2E conforme AGENTS. Nenhum ensaio usa o banco reservado para a demonstração até liberação do PO.

### Plano de implementação 2a — duas executoras e QA, com dono único por arquivo

É um plano de execução após liberação dos pré-requisitos, não autorização para iniciar agentes novos. A frente possui **Forja e Ponte como executoras e Prova como QA independente**, conforme GOALS; a worktree compartilhada é metodo-git com dono único por arquivo e checkpoints de pull/commit. A Forja é a **integradora única**, congela contratos, detém pontos compartilhados e faz A mais a parte UI de C; Ponte faz B mais MCP de C. Prova não escreve produto. Esse mapeamento não exige terceiro/quarto executor. Caminhos novos estão marcados como propostos. Nenhum executor edita arquivo atribuído a outro, mesmo para corrigir compilação: solicita ajuste ao dono via Maestro. Não editar migrations em paralelo.

**Ordem e dependências:** F0 (gates/contratos) → [A, B, C em paralelo] → F1 (integração, schema e chamadas centrais) → F2 (validação conjunta/aceite 2a). C usa contratos/mock HTTP congelados em F0 enquanto A/B implementam. B testa com fake da política/identidade de A; isso não substitui integração real em F2. E2E só roda após API, UI e schema reunidos em ambiente autorizado.

| Fatia/dono | Arquivos exclusivos e entrega | Dependências e testes da fatia |
|---|---|---|
| **F0 — Forja integradora, antes do paralelo** | História/catálogo/contexto/roadmap se aprovados, esta spec e contratos novos propostos `Application/Interfaces/IMethodAccessContext.cs`, `IMethodTransitionPolicy.cs`, `IMethodGitStore.cs`, `IGitProvider.cs`, **`IMethodMoveService.cs`**, DTOs em `Application/Features/MethodGit/Contracts/`; congelar formatos de token opaco, identidade, erros, links e evento normalizado | Aprovar G-SPEC/G-MIGRATION e PDs relevantes; revisar D101, contrato da App e desenho SQL. Contratos compilam antes de despachar; nenhum executante altera essa lista |
| **A — Forja: identidade e política** | Novos `Domain/Entities/MethodSecurity.cs`, `Application/Features/MethodSecurity/`, `Infrastructure/Identity/MethodTokens/`, `Infrastructure/Persistence/Configurations/MethodSecurityConfiguration.cs`, `Api/Authentication/MethodTokenAuthenticationHandler.cs`, `Api/Controllers/MethodTokensController.cs`; testes novos `Tests/MethodTokenTests.cs`, `Tests/MethodTransitionPolicyTests.cs`, `Tests/MethodTokenHttpTests.cs` | Depende F0. Emissão/revogação/expiração, principal/teto de permissões, matriz e bloqueio próprio; xUnit com relógio determinístico e HTTP de token. Entrega política reutilizável e pontos de integração, **sem editar Program/AppDbContext/handlers genéricos** |
| **B — Ponte: repositório e webhook** | Novos `Domain/Entities/MethodGit.cs`, `Application/Features/MethodGit/Repositories/`, `.../Webhooks/`, `Infrastructure/Services/MethodGit/`, `Infrastructure/Persistence/Configurations/MethodGitConfiguration.cs`, `Api/Controllers/MethodGitRepositoriesController.cs`, `MethodGitWebhookController.cs`, `MethodGitPullRequestsController.cs`; testes novos `Tests/GitWebhookTests.cs`, `GitLinkResolutionTests.cs`, `GitDeliverySqlTests.cs`, `GitTransitionTests.cs` | Depende F0 e interfaces da política A. GitHub App, cadastro/assinatura, inbox persistente, worker de retry, dedup/fato, resolver, transições e não vinculados; xUnit/SQL isolado e provedor falso. Sem scheduler de 2b, publicação/deploy de fase 3 ou edição dos handlers centrais |
| **C — Forja UI / Ponte MCP / Prova testes independentes** | Novos `Web/src/features/method-git/` (cliente HTTP próprio, tokens, repositórios, não vinculados e Vitest), `Web/e2e/method-git-phase2a.spec.ts`, fixtures próprias `Web/e2e/fixtures/method-git.ts`; existentes `tools/prisma-mcp/src/config.ts`, `api.ts`, `rules.ts`, `tasks.ts`, `setup.ts`, `import.ts`, `server.ts`, `tools/prisma-mcp/test/`, `README.md`, `DEMO.md` | Depende contratos F0; desenvolver contra mocks enquanto A/B avançam. Vitest, `npx tsc -b`/lint/build frontend e npm test/build MCP; testes garantem ausência de login/senha, papel servidor e leitura sem provisionamento. Não editar `services/api.ts`, `App.tsx` ou `ProjectSettings.tsx`; entregar componente para encaixe pelo integrador |
| **F1 — Forja integradora única, após A/B/C** | `Api/Program.cs`, arquivos existentes de registro DI, `Infrastructure/Persistence/AppDbContext.cs`, migration `Add_MethodGit_Phase2a`/Designer/ModelSnapshot; handlers existentes `Application/Features/WorkItems/Commands/{CreateWorkItemCommandHandler,MoveWorkItemCommandHandler}.cs`, `WorkItemManagementFeature.cs`, `WorkItemDetails/WorkItemDetailsFeature.cs` e demais caminhos identificados de escrita; middleware de contexto/auditoria existente; `Web/src/pages/ProjectSettings.tsx`, `Web/src/App.tsx` se necessário, `PROGRESS.md`/`NOTAS.md` | Somente após A/B/C entregarem. Encaixar autenticação/DI/EF/UI e política em **todas** as escritas, inclusive lote, duplicação, importação, troca de quadro e reclassificação. Gerar uma migration, revisar SQL/índices e ensaiar sob gate. Testes novos `Tests/MethodGenericWriteGuardTests.cs` + existentes afetados; eliminar bypass real antes de habilitar |
| **F2 — Forja e Prova QA, sem edição concorrente** | Revisão final dos pontos compartilhados, relatório de validação e correções devolvidas ao dono de cada arquivo | xUnit completo, MCP unitário/HTTP, Vitest, tipos/lint/build, SQL de concorrência e E2E desktop/mobile 2a mais suíte obrigatória. Provar API recusando agente/sistema sem guard MCP e UI humana mantendo suas regras atuais em projetos com e sem método. Só após aceite iniciar planejamento 2b |

Todos os caminhos abreviados começam em `src/Prisma.Workspace.*`, exceto `Tests/` (prefixo `tests/Prisma.Workspace.Tests/`) e `tools/`. Na fatia C, Forja detém `Web/src/features/method-git/**` e seus Vitest; Ponte detém **todo `tools/prisma-mcp/src/**`**; Prova detém `tools/prisma-mcp/test/**`, `README.md`/`DEMO.md`, os E2E/fixtures novos e `tests/Prisma.Workspace.Tests/MetodoGit/**` independentes. Testes A e `MethodGenericWriteGuardTests.cs` ficam com Forja; testes B ficam com Ponte. Arquivos DI adicionais, manifestos de dependências e lockfiles só podem ser atribuídos à Forja integradora; ela identifica nomes reais no início de F0, sem deixar a responsabilidade compartilhada. Se o levantamento dos caminhos genéricos descobrir arquivo novo, ele fica com F1 e é comunicado antes do paralelo, sem dois donos. O plano não instrui os executores a criar mais agentes e não mistura a execução 2b/3 com a primeira entrega.

### Inventário nominal de F0/F1 — nenhum caminho indireto fora da política

A Forja fecha e registra este inventário antes do paralelo. Todos os arquivos existentes abaixo são de F1/Forja; Ponte consome contratos novos e não os altera. Cada linha tem caso nominal em `tests/Prisma.Workspace.Tests/MethodGenericWriteGuardTests.cs`, com principal agente/sistema recusado quando o efeito escaparia da política e contraponto de sessão humana preservando o comportamento atual. Consultar somente `MoveWorkItemCommandHandler` não prova o requisito.

| Caminho existente | Escrita/efeito a provar |
|---|---|
| `Application/Features/WorkItems/Commands/CreateWorkItemCommandHandler.cs` | Criação e estágio inicial, inclusive quando o quadro padrão é resolvido; agente não cria autorizado |
| `Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs` | Move, reordenação com troca efetiva de estágio e origem/motivo confiáveis |
| `Application/Features/WorkItems/WorkItemManagementFeature.cs` | PUT com StageId, duplicação, custom fields/ID do método/PR e tentativa de trocar identidade para contorno |
| `Application/Features/WorkItemDetails/WorkItemDetailsFeature.cs` | Alteração de dados/taxonomia e efeitos que atingirem estado/identidade; sem autoridade administrativa emprestada |
| `Application/Features/Productivity/ProductivityFeature.cs` | Ações em massa e configuração/execução de regras; todos os itens validados na mesma autorização |
| `Infrastructure/Services/AutomationExecutor.cs` | Movimento causado por ação do agente mantém o principal agente e seu teto até o efeito final |
| `Infrastructure/Repositories/BoardStructureRepository.cs` | Transferência, remapeamento ao remover coluna/quadro, reclassificação e efeitos em descendentes |
| `Application/Features/Boards/BoardStructureFeature.cs` e `.../Commands/DeleteBoardCommandHandler.cs` | Entradas de transferência/exclusão de estrutura e destinos indiretos |
| `Application/Features/ExternalPortal/ExternalRequestTriageFeature.cs`, `ExternalPortalFeature.cs`, `ExternalFormsFeature.cs` | Triagem/roteamento/criação externa não fabricam autorização humana; identidade desconhecida não vira sistema |
| `Application/Features/Sprints/SprintsFeature.cs` | Efeitos da conclusão/cancelamento/movimentação de sprint que atinjam quadro/etapa/identidade |
| `Application/Features/Ai/` e `Infrastructure/Services/Ai/AiService.cs` (aplicação de sugestões quando existente) | Ação aplicada via assistente usa o contexto iniciador confiável, não ganha autoridade de sessão humana por ser chamada interna; D91 inalterada |
| `Infrastructure/Persistence/AppDbContext.cs` e repositórios de persistência de WorkItem/campos | Última barreira central também cobre escrita EF/indireta; criação/mudança/remove-ID sincronizam o índice sem autorizar execução |
| `Infrastructure/Identity/{ProjectAccessService,PermissionService,WorkItemAccessService,BoardAccessService}.cs` e `Api/Middleware/{OrganizationContextMiddleware,AuditContextMiddleware}.cs` | Principal não humano e escopo/teto corretos, sem exigir membro Identity sintético nem usar o emissor como autor; UI humana inalterada |

F0 registra ainda o handler/controlador chamador de cada caminho e o nome do caso de teste, verifica novos setters/SQL que surgirem e os atribui nominalmente à Forja antes de iniciar trabalho. `IMethodMoveService` tem implementação única em F1 para coluna, StageHistory, TaskEvent, autorização, fato consumido e estado de entrega na mesma transação; B não duplica essa unidade no worker. A autorização interativa e o contexto do principal não podem ser desligados por DTO/cabeçalho ou por executar automação/assistente no mesmo fluxo.

## Risks

- Aprovação aparente por criação de branch, vínculo ou papel declarado pelo cliente; mitigar com autorização central da API e teste de todos os caminhos de escrita.
- Exceção de retomada de bloqueio usada para iniciar trabalho não autorizado; exigir proveniência da execução e resposta humana verificável.
- Perda/reordenação de webhooks, múltiplos PRs e polling parcial causarem regressão ou conclusão precoce; usar fatos consumidos, validação de origem, concorrência por task e pendências explícitas.
- Credencial/segredo vazado ou URL de vínculo usada para SSRF; usar referências externas, hosts/repositórios permitidos, revogação, mascaramento e privilégios mínimos.
- Crescimento do histórico permanente e varreduras caras; separar retenção operacional, índices e cursor sem apagar prova de efeito aplicado.
- Sucesso de gate já publicado continuar no GitHub durante queda que impede publicar revogação; avaliar prazo de validade, detecção de indisponibilidade e procedimento operacional antes da fase 3.
- Pessoas com bypass ou credenciais capazes de forjar o contexto contornarem o gate; restringir emissor e revisar permissões/proteções com o administrador.
- Deploy/squash/rebase mal correlacionados concluírem task errada; exigir evidência verificável e não promover por nome do workflow.
- Contrato externo evoluir ou futuro provedor não ter equivalência completa; GitLab está fora deste ciclo, sem promessa de paridade, conforme D101.
- Confundir autorização do método com aprovação de ações do assistente IA (D91); aplicar a separação já decidida na D101 e testar que um contrato não amplia o outro.

## Rollback

Desabilitar consumo e agendamento por integração, preservar entregas pendentes, vínculos e histórico e mostrar que a automação está suspensa. Não mover tasks de volta automaticamente nem apagar fatos consumidos. Retomada começa por reconciliação controlada.

Na fase 3, suspender publicação não deve remover a proteção de branch automaticamente. Procedimento de contingência/bypass pertence ao administrador humano, é auditado e exige autorização própria. Reversão de schema futuro depende de G-MIGRATION e preservação de dados. Nesta revisão não há alteração operacional a reverter.

## Traceability

- Tarefas 8/9 do PO (2026-10-16) → contrato recebido `prisma.md`/`fluxo.md`/`local.md` e D100/D101 → **SPEC-METODO-GIT-INTEGRATION (review, git-spec-2)** → US-METODO-GIT-001 registrada no PR #23 e a integrar nesta branch → CA-2A-01 a 18, CA-2B-01 a 04, CA-3-01 a 04 → suítes propostas → commits futuros.
- Fase 1: adaptador `tools/prisma-mcp` na base desta branch; 2a: migração para token/política API e webhook; 2b: reconciliação; 3: gate/deploy. Código segue bloqueado por G-SPEC/G-MIGRATION e decisões pendentes aplicáveis, como determina D101.
- Bases internas: `AGENTS.md`, `DECISIONS.md`, `ROADMAP.md`, `context/index.yaml` e specs relacionadas em Dependencies. Este documento não altera o roadmap nem atribui aprovação a fase ainda não registrada.
- Referências técnicas públicas consultadas em 2026-10-16 estão vinculadas em RF-02/RF-03/RF-06/RF-07 e Contracts; nenhum segredo ou conteúdo privado foi enviado às consultas.
