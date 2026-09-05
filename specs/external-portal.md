# SPEC-EXT-001: Portal Externo de Solicitações

**Status:** approved

**Revisão funcional:** aprovada por PO durante a revisão módulo a módulo em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Definir o comportamento oficial do portal público usado por pessoas externas para abrir e acompanhar solicitações que se tornam tarefas internas, registrando separadamente o contrato aprovado, o que já existe no produto e os gaps de implementação.

## Contexto

O sistema já implementa portal público, formulários configuráveis, protocolo, chave de acompanhamento, mensagens, anexos e criação de `WorkItem`. Nesta revisão, PO definiu o contrato funcional desejado do módulo. A aprovação desta spec confirma os requisitos; não significa que o produto atual já os atende integralmente.

As solicitações externas continuam usando o mesmo `WorkItem` operado internamente, conforme D16 e D21. O formulário é somente a porta pública de entrada e o acompanhamento externo é uma visão autorizada da tarefa criada.

## Escopo incluído

- Formulários públicos acessíveis sem conta interna.
- Campos e validações configuráveis por formulário.
- Roteamento obrigatório para projeto, quadro, coluna e responsável.
- Criação direta de tarefa a partir de cada submissão.
- Protocolo e link secreto para acompanhamento.
- Consulta do status e do conteúdo funcional da tarefa pai.
- Envio e consulta de mensagens e anexos pelo solicitante.
- Proteção por rate limiting.
- Registro dos gaps entre o contrato aprovado e o produto atual.

## Escopo excluído

- Acesso do solicitante às áreas internas de administração.
- Exibição de subtarefas no portal externo.
- Conteúdo técnico, segredos, tokens, chaves, caminhos físicos, logs e dados de infraestrutura.
- CAPTCHA, honeypot ou bloqueio baseado em tempo mínimo de preenchimento.
- Expiração automática do link secreto de acompanhamento.
- Antimalware nesta primeira versão, conforme `SPEC-ATTACHMENTS`.

## Contrato funcional aprovado

### Acesso e envio público

- Uma pessoa externa pode abrir uma solicitação por formulário público sem possuir conta interna.
- O envio não depende de convite, login interno, aprovação ou triagem prévia para criar a tarefa.
- A submissão válida deve criar diretamente, em uma única operação, a solicitação externa e o `WorkItem` correspondente.
- O formulário público deve ser protegido contra abuso somente por rate limiting.
- CAPTCHA, honeypot e tempo mínimo de preenchimento não fazem parte do contrato aprovado.

### Formulários e validações

- Os campos apresentados ao solicitante são totalmente configuráveis.
- Não existe um conjunto global de campos públicos obrigatórios.
- Cada formulário define quais campos possui, quais são obrigatórios e suas próprias validações.
- Campos removidos ou opcionais em um formulário não podem ser exigidos implicitamente pela API.
- A configuração interna de destino não é um campo público do solicitante.

### Destino obrigatório

Para ser publicado, cada formulário deve possuir um destino completo e válido:

- projeto;
- quadro;
- coluna;
- responsável principal.

A tarefa deve ser criada diretamente nesse destino. Não é permitido publicar formulário com qualquer um desses quatro vínculos ausente, inválido, arquivado ou inacessível.

### Protocolo e acompanhamento

- Após uma submissão válida, o solicitante recebe:
  - protocolo estável;
  - link de acompanhamento contendo ou associado a um token secreto.
- O token de acompanhamento não expira automaticamente.
- O token deve ser imprevisível, armazenado somente como hash e validado em comparação segura.
- Conhecer apenas o protocolo não concede acesso.
- O acompanhamento deve permitir:
  - consultar o status atual;
  - ler o conteúdo funcional da tarefa pai;
  - adicionar mensagens;
  - enviar e consultar anexos.

### Visibilidade externa

- O solicitante visualiza todo o conteúdo funcional da tarefa pai vinculada à solicitação.
- Não existe marcação de mensagem ou anexo privado dentro dessa tarefa: todo conteúdo funcional adicionado a ela é visível ao solicitante externo.
- Subtarefas e seus conteúdos não aparecem no acompanhamento externo.
- Dados técnicos e segredos permanecem sempre ocultos, incluindo tokens, hashes, identificadores de infraestrutura, caminhos físicos, logs, configurações, credenciais e metadados internos sem função para o solicitante.
- A API pública deve usar DTO próprio e nunca serializar diretamente entidades internas.

### Anexos

- O solicitante pode anexar arquivos na abertura e durante o acompanhamento.
- Os anexos externos seguem o contrato geral de `SPEC-ATTACHMENTS`:
  - qualquer tipo de arquivo;
  - máximo de 10 MB por arquivo;
  - sem limite de quantidade por tarefa;
  - arquivos de mesmo nome permanecem separados;
  - sem inspeção antimalware nesta primeira versão.
- O acesso público a anexos exige protocolo e token de acompanhamento válidos.
- O nome físico e o caminho de armazenamento nunca são expostos.

### Proteção contra abuso

- O formulário e as mutações públicas devem possuir rate limiting por origem e rota.
- Ao exceder o limite, a API deve responder `429` sem persistir envio parcial.
- Não haverá CAPTCHA, honeypot ou exigência de tempo mínimo de preenchimento.
- Validação de payload, tamanho, autorização por token e tratamento seguro de arquivos continuam obrigatórios; não são mecanismos antiabuso opcionais.

## Invariantes

- Uma submissão bem-sucedida sempre corresponde a exatamente uma solicitação externa e uma tarefa interna.
- Nenhuma tarefa externa é criada sem projeto, quadro, coluna e responsável válidos.
- O protocolo sozinho nunca autoriza o acompanhamento.
- O token secreto nunca é persistido em texto puro nem devolvido por consultas posteriores.
- Subtarefas nunca atravessam a fronteira pública.
- Dados técnicos e segredos nunca atravessam a fronteira pública.
- Mensagens e anexos funcionais da tarefa pai não possuem estado privado nesta versão.
- Uma falha durante criação ou upload não pode deixar solicitação, tarefa ou arquivo parcialmente persistido.

## Estado atual comprovado no código

- `PublicExternalPortalController` possui rotas anônimas para consultar portal/formulário, submeter solicitações, acompanhar protocolo, responder e enviar/baixar anexos.
- `ExternalPortalController` permite configurar o portal e formulários internamente.
- Uma submissão cria `ExternalRequest` e `WorkItem` diretamente, preservando a origem externa.
- O portal atual pertence a um projeto e referencia um quadro; o formulário possui `InitialStageId` e `DefaultResponsibleId` opcionais.
- Os campos são persistidos em `FieldsJson`, possuem `IsRequired`, tipo, limites, regex, opções e condição simples.
- O código exige globalmente ao menos um campo e a presença do campo semântico `Subject`; o template padrão também marca nome, e-mail, assunto e descrição como obrigatórios.
- A coluna inicial e o responsável podem ficar ausentes: o handler tenta escolher uma coluna por fallback e aceita `ResponsibleId` nulo.
- O protocolo e a chave de acompanhamento são gerados na criação; somente o hash da chave é persistido.
- `ExternalRequest` não possui data de expiração da chave de acompanhamento.
- A consulta pública atual usa `PublicExternalRequestDto` mínimo com protocolo, título, descrição, status, categoria, serviço relacionado, datas, avaliação, mensagens e anexos marcados com `IsExternalVisible`.
- Comentários internos, campos adicionais da tarefa, responsáveis, horas e outros dados funcionais não são retornados atualmente.
- Mensagens externas e internas são persistidas em `ExternalRequestMessage`; comentários internos permanecem separados.
- Anexos públicos atuais dependem de `IsExternalVisible`.
- O envio de formulário usa lista configurável de extensões e MIME types, limite configurável de quantidade entre 0 e 20 e tamanho configurável entre 100 KB e 50 MB.
- Existem políticas `external-submissions` e `external-public` de rate limiting.
- Além do rate limiting, o produto atual usa honeypot (`Website`) e valida tempo mínimo de preenchimento (`MinimumCompletionSeconds`).
- Não foi encontrada evidência de CAPTCHA.

## Gaps entre a spec e o produto atual

1. **Campos globais:** o código exige pelo menos um campo e um campo `Subject`; o contrato não possui campo público globalmente obrigatório.
2. **Destino completo:** `InitialStageId` e `DefaultResponsibleId` são opcionais; o contrato exige projeto, quadro, coluna e responsável antes da publicação.
3. **Fallback de coluna:** o código pode escolher automaticamente uma coluna; o contrato exige a coluna configurada explicitamente no formulário.
4. **Responsável ausente:** a tarefa atual pode ser criada sem responsável; o contrato proíbe essa condição.
5. **Conteúdo integral:** o DTO público atual é deliberadamente mínimo e não apresenta todo o conteúdo funcional da tarefa pai.
6. **Privacidade por item:** o código filtra anexos por `IsExternalVisible` e separa comentários internos; o contrato determina que mensagens e anexos funcionais da tarefa pai não tenham conteúdo privado.
7. **Anexos:** o código limita quantidade e usa allowlists de extensão/MIME; o contrato aceita qualquer tipo, sem limite de quantidade e com máximo de 10 MB por arquivo.
8. **Proteção antiabuso:** honeypot e tempo mínimo existem hoje, mas o contrato aprovado mantém somente rate limiting.
9. **Decisões anteriores:** a visibilidade integral conflita com o DTO mínimo e a fronteira privada da D28; a remoção de honeypot/tempo mínimo/allowlists conflita parcialmente com D27. Essas ADRs precisam ser formalmente sucedidas antes da alteração do código.
10. **Cobertura:** não há matriz consolidada de testes e homologação manual comprovando todo o contrato aprovado.

## Persistência esperada

- `ExternalRequest` continua vinculado ao mesmo `WorkItem` interno.
- O protocolo deve ser único e estável.
- A chave secreta deve continuar persistida somente como hash e sem expiração automática.
- A configuração do formulário precisa persistir projeto, quadro, coluna e responsável obrigatórios.
- Campos, obrigatoriedade e validações continuam declarativos por formulário.
- Mensagens e anexos permanecem associados à solicitação/tarefa e disponíveis no acompanhamento autorizado.
- Alterações de schema exigem `G-MIGRATION` antes da implementação.

## API esperada

- Consultar formulário publicado sem autenticação interna.
- Submeter formulário e receber `201` com protocolo e link de acompanhamento.
- Consultar acompanhamento mediante protocolo e token secreto.
- Adicionar mensagem mediante protocolo e token secreto.
- Enviar, listar e baixar anexos mediante protocolo e token secreto.
- Retornar `400/422` para campos inválidos, `404` sem revelar se protocolo ou token diverge, `413` para payload excessivo e `429` para rate limit.
- Não criar estado parcial quando destino, campo, arquivo ou autorização forem inválidos.

## Interface esperada

- O formulário público renderiza somente os campos configurados e suas validações.
- O sucesso mostra protocolo e ação clara para abrir o acompanhamento.
- O acompanhamento mostra status, conteúdo funcional da tarefa pai, mensagens e anexos.
- Subtarefas não aparecem nem são contabilizadas na interface pública.
- Não há controles de privacidade para mensagens ou anexos dessa tarefa externa.
- Erros de validação, destino indisponível, arquivo, token e rate limit devem ser claros sem expor detalhes internos.

## Segurança e riscos aceitos

- **Token sem expiração:** se o link vazar, o acesso permanece válido indefinidamente enquanto não existir revogação ou rotação. Links podem vazar por histórico do navegador, encaminhamento, screenshots, analytics, logs de proxy e cabeçalho `Referer`.
- **Entropia e força bruta:** uma chave curta aumenta o impacto de tentativas automatizadas; rate limiting reduz, mas não elimina, esse risco.
- **Exposição integral:** todo conteúdo funcional da tarefa pai ficará disponível a quem possuir o link. Usuários internos podem publicar acidentalmente dados pessoais ou institucionais inadequados, pois não haverá marcação privada.
- **Anexos sem allowlist ou antimalware:** aceitar qualquer tipo aumenta o risco de armazenar e distribuir conteúdo malicioso; downloads devem impedir execução pelo servidor e usar cabeçalhos seguros.
- **Sem CAPTCHA/honeypot:** somente rate limiting pode ser insuficiente contra abuso distribuído ou ataques usando muitos endereços.
- **Token na URL:** a implementação deve evitar registrar a chave em logs e reduzir propagação por referenciadores, histórico e telemetria.
- As decisões acima foram aprovadas funcionalmente por PO; os riscos são registrados sem contrariá-las.

## Critérios de aceite

- **Dado** um visitante sem conta interna, **quando** submeter um formulário público válido, **então** uma solicitação e uma tarefa são criadas diretamente e ele recebe protocolo e link secreto.
- **Dado** um formulário sem um campo público específico, **quando** ele for submetido, **então** a API não exige esse campo por regra global.
- **Dado** um formulário sem projeto, quadro, coluna ou responsável válido, **quando** um administrador tentar publicá-lo, **então** a publicação é recusada.
- **Dado** um protocolo sem token válido, **quando** alguém tentar acompanhar a solicitação, **então** o acesso é recusado sem revelar dados.
- **Dado** um protocolo e token válidos, **quando** o solicitante abrir o acompanhamento, **então** vê status e todo o conteúdo funcional da tarefa pai, exceto dados técnicos e segredos.
- **Dado** uma tarefa com subtarefas, **quando** o solicitante abrir o acompanhamento, **então** nenhuma subtarefa ou conteúdo dela é exibido.
- **Dado** uma mensagem ou anexo funcional adicionado à tarefa pai, **quando** o solicitante atualizar o acompanhamento, **então** o conteúdo aparece sem depender de marcação pública.
- **Dado** um arquivo de qualquer tipo com até 10 MB, **quando** o solicitante o enviar com token válido, **então** o upload é aceito conforme `SPEC-ATTACHMENTS`.
- **Dado** o limite de envios excedido, **quando** houver nova submissão, **então** a API responde `429` e não persiste estado parcial.
- **Dado** um token antigo ainda válido, **quando** usado após qualquer intervalo de tempo, **então** não é recusado apenas por idade.

## Testes e homologação necessários

- Teste público sem conta interna.
- Testes de formulários sem campos globais e com validações diferentes por formulário.
- Testes de recusa da publicação sem projeto, quadro, coluna ou responsável.
- Teste transacional de criação de `ExternalRequest` + `WorkItem` e rollback em falha.
- Testes de protocolo correto, token incorreto, token sem expiração e isolamento entre solicitações.
- Testes de DTO público garantindo conteúdo funcional da tarefa pai e bloqueio de dados técnicos/segredos.
- Teste comprovando que subtarefas não são retornadas.
- Testes de mensagens e anexos sem marcação privada.
- Testes de anexos alinhados a `SPEC-ATTACHMENTS`, inclusive 10 MB, qualquer tipo e múltiplos nomes iguais.
- Teste de rate limiting com resposta `429` e ausência de persistência parcial.
- Teste comprovando ausência de dependência de CAPTCHA, honeypot e tempo mínimo.
- Revisão de segurança específica para token permanente e exposição integral do conteúdo.
- Homologação manual por PO ainda pendente para criação, acompanhamento, mensagens e anexos.

## Decisões técnicas ainda pendentes

- Mecanismo de revogação ou rotação de um link comprometido, sem expiração automática por tempo.
- Entropia mínima e formato definitivo do token secreto.
- Política de cabeçalhos e redaction para impedir vazamento do token em logs, analytics e `Referer`.
- Definição exata do DTO de “todo conteúdo funcional”, preservando a exclusão de subtarefas e segredos.
- Estratégia operacional contra abuso distribuído quando rate limiting por origem for insuficiente.

Esses pontos não reabrem as decisões funcionais aprovadas; precisam ser fechados antes da implementação quando afetarem segurança, API, schema ou operação.

## Referências

- `AGENTS.md`
- `DECISIONS.md`, especialmente D8, D16, D17, D18, D21, D26, D27, D28 e D34.
- `ROADMAP.md`, Fase 7.
- `specs/attachments.md`
- `specs/user-access-permissions.md`
- `src/Detran.Kanban.Api/Controllers/ExternalPortalController.cs`
- `src/Detran.Kanban.Api/Program.cs`
- `src/Detran.Kanban.Domain/Entities/ExternalPortal.cs`
- `src/Detran.Kanban.Domain/Entities/ExternalForm.cs`
- `src/Detran.Kanban.Application/Features/ExternalPortal/ExternalPortalFeature.cs`
- `src/Detran.Kanban.Application/Features/ExternalPortal/ExternalFormsFeature.cs`

## Rollback

Esta revisão altera somente documentação. A implementação futura deve possuir rollback para mudanças de schema e contratos públicos, preservar a tarefa e a solicitação já criadas e permitir desabilitar o formulário sem apagar histórico.

## Rastreabilidade

Decisões humanas de PO → `SPEC-EXT-001` aprovada → gaps documentados → futura tarefa de implementação aprovada → testes automatizados → homologação manual → evidência de aderência.
