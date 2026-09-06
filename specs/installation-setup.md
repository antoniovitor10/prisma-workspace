# SPEC-INSTALLATION-SETUP: Configuração inicial segura da instalação Community

**Status:** draft

## Objective

Definir o contrato para configurar, exatamente uma vez, a primeira conta administradora e a primeira organização de
uma instalação nova do Prisma WorkSpace Community, sem credenciais distribuídas, usuários existentes ou dados demo.

## Context

A `SPEC-OPEN-SOURCE-DISTRIBUTION` aprovada exige um bootstrap seguro do primeiro administrador. O estado atual
ainda executa seed de demonstração no ambiente Development e não oferece um contrato público seguro de setup. Esta
spec separa o setup real do seed opcional e o mantém bloqueado até aprovação humana, conforme D67.

## Scope

- Instalação normal sem usuários, organizações, memberships ou dados de demonstração preexistentes.
- Consulta anônima do estado mínimo de setup por `GET /api/setup/status`.
- Configuração inicial anônima por `POST /api/setup`, protegida por token externo forte e configuração explícita.
- Estado singleton persistido da instalação e criação atômica da primeira identidade, organização e membership
  `Administrator`, condicionados a `G-MIGRATION`.
- Indisponibilidade permanente do setup após sua primeira conclusão.
- Seed demo limitado a Development, opt-in explícito, neutro e isolado do setup real.
- UI guiada em `/setup` e cobertura E2E contra SQL Server real no lote de implementação.

## Out of Scope

- Recuperação, reinicialização ou reparação de uma instalação já configurada.
- SSO, MFA, SMTP completo, envio de e-mail ou recuperação de senha.
- Produção, deploy, hospedagem, licença e publicação do repositório.
- Criação de usuários, organização ou dados demo em uma instalação existente.

## Functional Requirements

1. `Setup:Enabled` e `Setup:Token` devem ser configurações externas explícitas, mapeadas no Compose por
   `PRISMA_SETUP_ENABLED` e `PRISMA_SETUP_TOKEN`; setup fica indisponível quando a flag não é `true` ou o token é inválido.
2. `GET /api/setup/status` deve permitir acesso anônimo e expor somente `initialized` e `setupAvailable`. Não pode
   expor e-mail, organização, slug, identificadores, configuração, token, motivo interno ou existência de dados demo.
3. `initialized` vem de um registro singleton `InstallationState`, criado pela migration com estado não inicializado;
   `setupAvailable` só é `true` quando a configuração está válida e `InstallationState.IsInitialized` é `false`.
4. `POST /api/setup` deve receber o token somente no header `X-Prisma-Setup-Token`. O corpo usa exatamente
   `{ administratorName, administratorEmail, administratorPassword, organizationName, organizationSlug }`, com todos
   os campos obrigatórios e validados antes da persistência. Token em query string, URL, cookie ou corpo é proibido.
5. Com token e campos válidos e setup disponível, o POST cria exatamente uma vez: um `IdentityUser` com e-mail
   confirmado, uma `Organization` e um `OrganizationMember` ativo com papel `Administrator`, e marca a instalação
   como inicializada. A confirmação automática é a proposta recomendada para permitir login sem SMTP no primeiro uso.
6. A criação da identidade, organização e membership deve ser uma única unidade atômica. Uma falha em qualquer etapa
   não pode deixar usuário, organização ou membership parcial.
7. O token deve vir exclusivamente de fonte externa de configuração/segredo e ser comparado de modo apropriado para
   segredo. Seu valor em texto claro nunca pode ser logado, persistido, incluído em exceções, auditoria ou resposta.
8. Depois do commit inicial, `InstallationState.IsInitialized` nunca pode voltar a `false`; desativação, mudança de papel,
   alteração de configuração ou resposta perdida não reabrem o setup. Recuperação administrativa usa outro fluxo futuro.
9. `POST /api/setup` deve abrir transação SQL Server `Serializable`, adquirir lock exclusivo sobre o singleton, revalidar
   o estado dentro da transação e compartilhar a mesma transação entre ASP.NET Identity e o domínio. Apenas uma chamada
   pode concluir; as demais retornam conflito controlado e todo registro da chamada perdedora sofre rollback.
10. `Setup:Token` deve conter pelo menos 32 bytes aleatórios, representados em base64url, e ser comparado em tempo fixo.
    Seu header deve ser redigido por logging/tracing e nunca pode aparecer em log, exceção, auditoria, telemetria ou resposta.
11. `POST /api/setup` usa rate limit de 5 tentativas por 15 minutos por cliente. Flag/token ausente, token inválido e setup
    desabilitado retornam a mesma resposta `403 setup_unavailable`; setup concluído ou corrida perdida retornam
    `409 setup_already_completed`; validação retorna `400`; sucesso retorna `201 { initialized: true }`.
12. Seed demo não é parte do setup: usa `Seed:DemoEnabled=false` por padrão e `Seed:DemoPassword` externo; só roda em
    `Development` quando habilitado explicitamente e o banco, após migrations, não contém usuários, organizações,
    memberships ou dados de produto. Caso contrário, falha antes de mutar qualquer dado com diagnóstico acionável.
13. O dataset demo deve ser neutro: organização `Prisma Demo`, identidades sob `example.invalid` e exemplos genéricos de
    produto/projeto, sem nomes, domínios, órgãos, locais, processos ou remediações institucionais.
14. Quando autorizado, o seed demo deve criar sua cadeia e marcar `InstallationState` como inicializado na mesma
    transação, mantendo `/api/setup` indisponível e usando senha apenas da configuração externa.
15. `administratorName` preenche `OrganizationMember.DisplayName`; a identidade continua usando o e-mail como login.
16. A UI `/setup` consulta o status, solicita o token sem persistir/reexibir, envia-o no header redigido e, após `201`,
    direciona ao login. Estados `403` e `409` possuem orientação clara e nenhum dado sensível.
17. Administradores adicionais continuam sendo criados somente pelos fluxos autenticados de organização/convite; a
    proibição cobre novas execuções do setup, não a administração legítima posterior.

## Invariants

- Uma instalação normal nova começa sem usuário, organização, membership e conteúdo demo.
- O token externo nunca é persistido, respondido ou registrado em log.
- A resposta de status contém somente `initialized` e `setupAvailable`.
- A primeira configuração cria uma única cadeia consistente de usuário, organização e Administrator ativo.
- Não existe segundo administrador inicial por repetição, corrida, alteração posterior de configuração ou seed demo.
- Seed demo não cria nem altera dados fora de Development e nunca modifica uma instalação existente.
- O marcador singleton é imutável depois de inicializado e sua migration só pode ser criada após `G-MIGRATION` humano.

## User Story References

- `US-INSTALLATION-SETUP-001` (`stories/US-INSTALLATION-SETUP-001.md`)
- `US-OPEN-SOURCE-001` (`stories/US-OPEN-SOURCE-001.md`)

## Acceptance Criteria

- **Given** uma instalação vazia com `Setup:Enabled=true` e token externo válido
  **When** `POST /api/setup` recebe dados válidos
  **Then** retorna `201`, cria uma única identidade confirmada, organização e membership Administrator, marca o singleton
  como inicializado e não cria dados demo nem expõe o token

- **Given** setup desabilitado, token ausente ou token inválido
  **When** um visitante consulta ou envia setup
  **Then** o status não revela segredos e o POST retorna o mesmo `403 setup_unavailable` sem persistir dados

- **Given** um setup inicial já concluído
  **When** `GET /api/setup/status` ou `POST /api/setup` é chamado
  **Then** o status informa `{ initialized: true, setupAvailable: false }` e o POST retorna
  `409 setup_already_completed` sem criar outra cadeia inicial

- **Given** duas requisições válidas concorrentes em uma instalação nova
  **When** ambas tentam completar o setup
  **Then** uma única transação vence e a outra recebe `409`, sem registros adicionais ou parciais no SQL Server

- **Given** que o commit do setup ocorreu mas a resposta foi perdida
  **When** o cliente repete a chamada
  **Then** recebe `409 setup_already_completed` e nenhum usuário, organização ou membership é duplicado

- **Given** uma instalação existente ou ambiente diferente de Development
  **When** o seed demo é avaliado
  **Then** ele não é executado nem modifica dados; em Development vazio, só pode rodar após opt-in explícito com dataset neutro

## Data Impact

Há migration planejada e bloqueada em `G-MIGRATION`: tabela singleton `InstallationState`, com chave fixa, campo
`IsInitialized`, `InitializedAt` anulável e concurrency token. A migration cria exatamente um registro não inicializado.
Identity, Organization, OrganizationMember e a transição do singleton devem compartilhar uma transação. Nenhuma migration
existente será editada; o nome e o DDL finais serão revisados no gate.

## Authorization Impact

Os dois endpoints são anônimos apenas para a configuração inicial: o token externo é o requisito de autorização do
POST. O primeiro membership recebe exclusivamente o papel organizacional `Administrator` já existente; setup não
concede autoridade de administrador da plataforma nem altera RBAC posterior. Após o setup inicial, nenhum visitante
ou usuário autenticado pode reutilizar o endpoint para elevar privilégio.

## Contracts

| Endpoint | Acesso | Contrato |
|---|---|---|
| `GET /api/setup/status` | anônimo | Responde exclusivamente `{ initialized, setupAvailable }`. |
| `POST /api/setup` | anônimo + `X-Prisma-Setup-Token` | Recebe o payload definido nesta spec; retorna `201 { initialized: true }` ao criar a cadeia inicial uma única vez. |

Erros seguem Problem Details com código público estável e sem revelar se e-mail ou slug já existem. `403 setup_unavailable`
uniformiza flag/token ausente, inválido ou desabilitado; `409 setup_already_completed` cobre estado concluído e corrida
perdida; conflitos de dados anteriores retornam `409 setup_conflict`; falhas internas retornam Problem Details sem PII.

## Dependencies

- D58 e `SPEC-ORGANIZATIONS` para Organization e papéis organizacionais.
- `SPEC-AUTH-001` para Identity, senha, confirmação de e-mail, redaction e segurança.
- `SPEC-OPEN-SOURCE-DISTRIBUTION` e D75/D77 para instalação Community independente.
- Aprovação humana de `G-SPEC` antes de código, UI, endpoint, seed ou testes E2E.

## Error Cases

- `Setup:Enabled` ausente ou falso: setup indisponível, sem persistência e sem revelar configuração.
- Token ausente ou inválido: recusa segura, sem token em log, resposta, banco ou auditoria.
- Campos ausentes/inválidos ou slug em conflito: nenhuma persistência parcial.
- Administrador ativo ou corrida perdida: resultado controlado de indisponibilidade/conflito e nenhuma segunda cadeia inicial.
- Falha de Identity, banco ou transação: rollback integral e erro seguro sem segredos.
- Seed solicitado fora de Development, sem opt-in ou em instalação existente: não executar e não mutar dados.

## Pending Decisions

- O `G-SPEC` deve aceitar ou rejeitar conjuntamente as propostas desta versão: e-mail inicial confirmado, header/payload,
  códigos HTTP, política do token, rota `/setup`, seed Development opt-in e dataset neutro.
- O `G-MIGRATION` deve revisar tabela singleton, chave fixa, concurrency token, lock/isolamento SQL Server e rollback.
- Textos visuais finais da UI podem ser refinados dentro da identidade D68 sem mudar o contrato funcional aprovado.

## Test Gate Mapping

- Backend unitário/integração: status mínimo, configuração desabilitada, validações, rate limit, token redigido,
  atomicidade, repetição após resposta perdida, corrida e indisponibilidade permanente.
- Persistência em SQL Server real: lock concorrente, rollback com falha injetada após cada etapa e confirmação de que não
  há usuário/organização/membership parcial. Testes InMemory não satisfazem este gate.
- Seed: Development + opt-in, dataset neutro e não mutação de instalação existente.
- Frontend e E2E futuros: primeiro acesso guiado, respostas de conflito/indisponibilidade e ausência de dados demo.
- Gates obrigatórios futuros: `G-SPEC`, `G-MIGRATION`, backend build/test e frontend build/test/lint/E2E.

## Risks

- Um token configurado de modo fraco, exposto ou registrado pode permitir tomada da instalação.
- Falta de atomicidade ou revalidação concorrente pode criar cadeias iniciais inconsistentes.
- Um seed implícito ou institucional compromete a instalação limpa e a distribuição Community.
- Confundir Administrator organizacional com administrador da plataforma quebraria a fronteira de autorização da D58.

## Rollback

Antes de o primeiro setup concluir não deve haver dados persistidos parciais. Após a criação bem-sucedida, não existe
rollback automático, reset nem reabertura do endpoint; recuperação de instalação é explicitamente fora do escopo e
exige nova história, spec e decisão humana.

## Human Gates

- `G-SPEC`: **pending** — bloqueia integralmente implementação de endpoint, seed, UI e testes de comportamento.
- `G-MIGRATION`: **pending** — obrigatório para o singleton e proibido antes de aprovação humana.
- `G-COMPLETION`: conforme a classificação futura do lote de implementação.

## Traceability

US-INSTALLATION-SETUP-001 → SPEC-INSTALLATION-SETUP → TASK-042 → setup/status + setup atômico + seed opt-in + UI/E2E futuros
