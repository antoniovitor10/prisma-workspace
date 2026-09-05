# SPEC-AUTH-001: Autenticação, Sessão, Tokens e Segurança Multi-Tenant

**Status:** approved
**Revisão humana:** contrato atualizado com as decisões explícitas de PO durante a revisão módulo a módulo.
**Leitura:** esta spec separa o contrato aprovado, o estado comprovado no código e os gaps que ainda precisam de implementação e homologação.

## Objective

Definir o comportamento aprovado para cadastro, autenticação, sessão, renovação de tokens, recuperação de senha e isolamento por organização, mantendo explícitas as diferenças entre esse contrato e o produto atual.

## Context

A API usa ASP.NET Core Identity, JWT Bearer e refresh token persistido. O tenant não integra o JWT: ele é transportado por `X-Organization-Id`, validado para cada requisição autenticada de negócio e aplicado ao `AppDbContext`. A D35 define o hardening original de autenticação e tokens; D17/D58 definem isolamento, membership única do usuário comum e seleção administrativa entre tenants.

Esta revisão humana não autoriza alteração imediata de código. Os itens divergentes devem ser implementados e testados em tarefa própria, respeitando os gates aplicáveis.

## Scope

### Incluído

- Cadastro, confirmação de e-mail, login, refresh, logout e recuperação/redefinição de senha.
- Coleta e validação do nome completo obrigatório no cadastro de novos usuários.
- Emissão, validação, rotação, expiração, revogação e retenção de tokens de sessão.
- Regras de senha, tentativas inválidas, sessões simultâneas e encerramento da sessão.
- Desativação da única associação organizacional sem apagar ou bloquear globalmente a identidade de login.
- Configurações obrigatórias de autenticação e banco.
- Isolamento por organização, rate limiting, redaction de segredos e erros de autenticação.

### Fora do escopo

- SSO, MFA ou provedor institucional de identidade.
- Definição detalhada das permissões funcionais, coberta por `SPEC-USER-ACCESS-PERMISSIONS`.
- Configuração concreta de SMTP, cofre de segredos, proxy e origens de produção.
- Implementação dos gaps listados nesta spec.

## Contrato aprovado por PO

1. Desativar um membro remove o acesso ao seu único tenant sem apagar ou bloquear globalmente a identidade. O usuário comum não pode possuir outra associação organizacional; após a desativação, a sessão pode continuar autenticada, mas não acessa APIs funcionais de organização.
2. Registros de sessões encerradas, revogadas ou expiradas permanecem armazenados indefinidamente. Não há limpeza automática por prazo.
3. Quando o mesmo refresh token é apresentado quase simultaneamente, apenas a primeira renovação é aceita. A segunda é recusada, sem emitir outro token e sem revogar toda a sessão ou família.
4. A ausência de qualquer configuração obrigatória de segurança impede a inicialização. Isso inclui, no mínimo, chave JWT válida, issuer, audience e connection string obrigatória.
5. O cadastro com um e-mail já existente informa claramente que o e-mail já está cadastrado.
6. A confirmação de e-mail é obrigatória antes do primeiro login.
7. Tentativas incorretas de senha não bloqueiam automaticamente a conta. O rate limiting continua protegendo o endpoint contra excesso de tentativas.
8. A senha possui mínimo de 8 caracteres, sem exigência adicional obrigatória de maiúscula, minúscula, número ou símbolo.
9. A sessão não deve permanecer autenticada após o navegador ser completamente fechado.
10. O mesmo usuário pode manter sessões simultâneas em vários dispositivos.
11. Redefinir a senha encerra todas as sessões ativas do usuário.
12. Todo novo cadastro exige nome completo com pelo menos nome e sobrenome. O nome é a identificação principal na interface; o e-mail só substitui o nome para dados legados sem nome.

## Atores e permissões

- **Visitante anônimo:** pode cadastrar-se, confirmar e-mail, entrar e solicitar recuperação, sujeito ao rate limiting.
- **Usuário não confirmado:** pode confirmar o e-mail e solicitar recuperação, mas não pode fazer login.
- **Usuário confirmado:** pode autenticar-se e manter sessões simultâneas em diferentes dispositivos.
- **Membro ativo:** pode usar sua única organização enquanto a associação estiver ativa.
- **Membro desativado:** mantém a identidade global, mas perde acesso funcional por não possuir tenant ativo.
- **Administrador da plataforma:** pode selecionar tenants por autoridade de control plane conforme `SPEC-ORGANIZATIONS`, sem adquirir múltiplas memberships comuns.
- **Operador/ambiente:** deve fornecer todas as configurações obrigatórias antes da inicialização.

## Estado atual comprovado no código

1. O cadastro cria um `IdentityUser`, exige e-mail único, gera token nativo de confirmação e retorna `202`.
2. A confirmação de e-mail antes do login já é obrigatória por `RequireConfirmedEmail = true`; login não confirmado retorna `email_not_confirmed`.
3. O produto atual exige senha mínima de 10 caracteres, dígito, minúscula, maiúscula e caractere não alfanumérico, tanto no Identity quanto nos contratos HTTP.
4. O login atual contabiliza falhas reais e bloqueia automaticamente após cinco falhas por 15 minutos.
5. O JWT é HMAC-SHA256 e valida issuer, audience, validade e assinatura. A chave JWT inválida e a connection string ausente já abortam a inicialização; issuer e audience vazios ainda não são rejeitados explicitamente no bootstrap.
6. O refresh token é opaco, entregue em cookie HttpOnly e persistido apenas como hash SHA-256. A entidade registra família, usuário, expiração, revogação, substituto, IPs e `RowVersion`.
7. O comportamento atual aceita a reapresentação de um token rotacionado dentro de uma tolerância padrão de 30 segundos e emite outro token na mesma família. Isso diverge do contrato aprovado, que deve recusar a segunda renovação sem derrubar a família.
8. Logout revoga a família correspondente. Redefinir a senha já revoga todos os refresh tokens ainda não revogados do usuário.
9. Não foi encontrada rotina de limpeza física de refresh tokens expirados ou revogados; portanto, a retenção indefinida já ocorre por ausência de limpeza, embora não haja teste de retenção.
10. A associação ativa é validada a cada requisição de negócio. Desativar o membro impede seu único tenant sem revogar automaticamente a identidade ou as sessões globais, que permanecem sem acesso funcional.
11. O frontend guarda o access token em `localStorage`, e o cookie de refresh recebe data de expiração. Assim, a sessão atual pode sobreviver ao fechamento completo do navegador, divergindo do contrato aprovado.
12. Não existe limitação global a um único dispositivo; sessões de famílias diferentes podem coexistir.
13. O cadastro repassa descrições do ASP.NET Core Identity. A existência de mensagem clara e estável em português para e-mail duplicado não está comprovada como contrato testado.
14. Há rate limiting global de 300 requisições/minuto por usuário ou IP e limite de autenticação de 30 requisições/15 minutos por IP.
15. O tenant é validado por `X-Organization-Id`; membro e organização precisam estar ativos. Filtros globais e proteção de escrita impedem leitura ou escrita cruzada entre organizações.
16. Logs e auditoria aplicam redaction a propriedades identificadas como sensíveis. Erros conhecidos são traduzidos para Problem Details.
17. O cadastro atual recebe somente e-mail e senha; não coleta, valida nem preserva nome completo.

## Gaps entre contrato aprovado e implementação

| Tema | Contrato aprovado | Estado atual | Situação |
|---|---|---|---|
| Refresh concorrente | aceita somente a primeira renovação; recusa a segunda sem revogar a família | reapresentação dentro de 30 segundos emite outro token | gap de backend e testes |
| Configuração obrigatória | chave, issuer, audience e conexão ausentes impedem startup | apenas chave e conexão são verificadas explicitamente no bootstrap | gap de configuração/teste |
| E-mail duplicado | informar claramente que já está cadastrado | descrição depende do Identity e não possui contrato estável comprovado | gap de API/UI/teste |
| Lockout | não bloquear automaticamente | bloqueia após 5 falhas por 15 minutos | gap de backend/API/UI/teste |
| Senha | mínimo de 8, sem composição obrigatória | mínimo de 10 e composição completa obrigatória | gap de backend/API/UI/teste |
| Encerramento do navegador | sessão termina ao fechar o navegador | access token em `localStorage` e refresh cookie persistente | gap de frontend/cookie/teste |
| Retenção de sessões | permanente | não existe limpeza automática comprovada | comportamento compatível, falta teste |
| Desativação por organização | perde apenas o tenant desativado | middleware valida associação ativa sem revogação global | comportamento compatível, falta homologação |
| Membership organizacional | usuário comum pertence a exatamente um tenant | banco permite o mesmo `UserId` em várias organizações | gap de domínio, persistência, convite e testes |
| Múltiplos dispositivos | permitido | não há limitação a uma sessão/família | comportamento compatível, falta homologação |
| Reset de senha | encerra todas as sessões | `RevokeAllForUserAsync` já é chamado | comportamento compatível, falta teste HTTP |
| Confirmação de e-mail | obrigatória antes do login | já configurada e verificada no login | comportamento compatível, falta E2E |
| Nome completo no cadastro | obrigatório, com nome e sobrenome | `RegisterRequest` e tela recebem somente e-mail e senha | gap de API, UI, persistência e testes |

## Functional Requirements

1. Cadastro válido deve exigir nome completo, e-mail e senha, criar uma conta pendente de confirmação e retornar `202`.
2. Cadastro com e-mail existente deve retornar uma mensagem clara informando que o e-mail já está cadastrado.
3. Usuário sem e-mail confirmado não pode fazer login.
4. Senhas de cadastro e redefinição devem aceitar no mínimo 8 caracteres e não exigir composição adicional.
5. Falhas de senha não devem ativar lockout automático; o rate limiting deve continuar ativo.
6. Access tokens devem validar issuer, audience, validade, assinatura e chave.
7. Toda configuração obrigatória deve ser validada antes de a aplicação iniciar.
8. Refresh tokens não podem ser persistidos em texto claro nem expostos no corpo normal da resposta.
9. Cada refresh token pode produzir no máximo uma renovação bem-sucedida. Uma tentativa duplicada deve ser recusada sem revogar os demais tokens válidos da família.
10. O usuário pode manter famílias de sessão simultâneas em dispositivos diferentes.
11. Fechar completamente o navegador deve encerrar a sessão local e impedir renovação automática posterior.
12. Logout encerra a sessão correspondente; redefinição de senha encerra todas as sessões do usuário.
13. Desativar a associação única impede acesso funcional ao tenant sem excluir a conta global.
14. Registros de sessões permanecem indefinidamente, inclusive depois de expiração ou revogação.
15. Requisições comuns devem ser vinculadas à única associação ativa do usuário; seleção entre organizações existe somente para Administrador da plataforma.
16. Dados organizacionais devem continuar isolados por filtros de leitura e proteção de escrita.
17. Erros e logs não podem revelar senhas, tokens, hashes, chaves ou segredos.
18. O nome completo do novo usuário deve conter pelo menos duas partes não vazias e permanecer disponível até a criação ou ativação da associação organizacional.
19. A ausência de nome em registros legados não bloqueia autenticação nem associação; nesses casos, o e-mail permanece fallback visual temporário conforme `SPEC-RESPONSIBLE-DISPLAY-NAME`.

## Invariants

- Uma conta não confirmada não estabelece sessão autenticada.
- Uma senha válida tem pelo menos 8 caracteres; composição adicional não é obrigatória.
- Falhas de senha não alteram a conta para estado de bloqueio automático.
- Um refresh token produz no máximo uma rotação aceita.
- A recusa de uma segunda renovação concorrente não revoga a família válida.
- Redefinir senha revoga todas as sessões ativas do usuário.
- Desativar a associação única não apaga a conta nem revoga obrigatoriamente as sessões, mas elimina acesso funcional a tenant.
- O mesmo login/e-mail de usuário comum não pode possuir associações em organizações diferentes.
- Um refresh token revogado ou expirado não é ativo, mas seu registro é preservado.
- O token em texto claro não é persistido em `RefreshToken.TokenHash`.
- Uma organização ou associação inativa não estabelece contexto válido para aquele tenant.
- Ausência de tenant não pode produzir leitura cruzada.
- Todo cadastro novo possui nome completo válido com, no mínimo, nome e sobrenome.
- E-mail não substitui visualmente um nome completo existente.

## Estados e transições

### Conta

- **Criada/não confirmada → confirmada:** confirmação bem-sucedida por token nativo do Identity.
- **Não confirmada + tentativa de login → acesso recusado:** nenhuma sessão é emitida.
- **Senha incorreta → nova tentativa permitida:** a falha está sujeita ao rate limiting, sem bloqueio automático da conta.
- **Senha vigente → senha redefinida:** todas as sessões ativas são revogadas.

### Refresh token e sessão

- **Emitido/ativo → rotacionado/revogado:** o primeiro uso válido gera um substituto na mesma família.
- **Rotacionado + reapresentação concorrente → recusado:** não emite substituto adicional e não revoga a família.
- **Ativo → revogado:** logout da sessão, redefinição de senha ou revogação administrativa futura explicitamente autorizada.
- **Ativo → expirado:** passagem de `ExpiresAt`; o registro permanece armazenado.
- **Navegador aberto → navegador completamente fechado:** sessão local encerrada.

### Contexto organizacional

- **Associação única ativa → válida:** usuário comum opera automaticamente seu tenant conforme permissões, sem seletor.
- **Associação ativa → desativada:** o tenant passa a ser recusado; conta e sessões globais permanecem, sem outro tenant funcional.
- **Platform Admin + tenant selecionado → contexto administrativo válido:** alternância ocorre pela autoridade de control plane, não por membership comum.
- **Contexto ausente/inválido → rejeitado:** header inválido produz `400`; associação ou organização indisponível produz `403`.

## Data Impact

Esta revisão não aplica migration. O contrato de retenção indefinida é compatível com a persistência atual. As mudanças de senha, lockout, validação de configuração, erro de cadastro e duração local da sessão podem ser implementadas sem mudança de schema. A correção de concorrência deve preservar `RowVersion` e atomicidade; qualquer conclusão diferente durante a implementação exige avaliação própria de `G-MIGRATION`.

## Persistência e entidades

- `RefreshToken`: `Id`, `FamilyId`, `UserId`, `TokenHash`, criação, expiração, revogação, substituto, IPs e `RowVersion`.
- Registros de `RefreshToken` expirados ou revogados não devem ser removidos automaticamente.
- `IdentityUser`: cadastro, confirmação, senha e tokens temporários continuam no ASP.NET Core Identity.
- O fluxo de cadastro deve preservar o nome completo até que ele esteja disponível na associação organizacional. A estratégia técnica não pode criar fontes funcionais divergentes de nome.
- `OrganizationMember`: usuário comum possui no máximo uma associação; sua desativação não representa bloqueio global da identidade.
- `AppDbContext`: mantém filtros globais e proteção de escritas organizacionais.

## Authorization Impact

- A desativação atinge a única associação do usuário comum, sem excluir sua conta global.
- Uma sessão autenticada não concede acesso a um tenant sem associação ativa.
- Múltiplas sessões e dispositivos são permitidos, mas todos são revogados após redefinição de senha.
- Políticas funcionais e permissões de recursos continuam fora desta spec.

## Contracts

### API de autenticação

- `POST /api/auth/register`: `RegisterRequest(fullName, email, password)`; `fullName` exige nome e sobrenome; `202` para criação; `400` com erros associados aos campos quando o nome, e-mail ou senha forem inválidos, ou mensagem clara quando o e-mail já existir.
- `POST /api/auth/confirm-email`: `ConfirmEmailRequest(userId, token)`; `204`; token ou usuário inválido retorna `400`.
- `POST /api/auth/login`: `LoginRequest(email, password)`; `200 AuthResponse` e cookie de sessão; `401` para credencial inválida ou e-mail não confirmado. Não deve retornar `423` por lockout automático de senha.
- `POST /api/auth/refresh`: aceita somente a primeira rotação de cada token; duplicata retorna `401` sem revogar a família válida.
- `POST /api/auth/logout`: revoga a família da sessão atual, remove o cookie e retorna `204`.
- `POST /api/auth/forgot-password`: continua sem revelar conta inexistente.
- `POST /api/auth/reset-password`: redefine a senha e revoga todas as sessões ativas.

### Headers e transporte

- `Authorization: Bearer <JWT>` para APIs autenticadas.
- `X-Organization-Id: <guid>` para APIs autenticadas sujeitas ao middleware.
- Refresh token permanece em cookie `HttpOnly` e `SameSite=Strict`.
- A forma exata de tornar o cookie não persistente e o armazenamento do access token devem ser definidas na tarefa de implementação, desde que o resultado observável seja encerrar a sessão ao fechar completamente o navegador.

## Interface

- Cadastro duplicado deve mostrar claramente “este e-mail já está cadastrado” ou mensagem semanticamente equivalente.
- Cadastro deve apresentar campo obrigatório de nome completo e indicar junto a ele quando nome ou sobrenome estiver ausente.
- Login não confirmado deve orientar a confirmação do e-mail.
- A interface não deve apresentar estado de bloqueio temporário por falhas de senha, pois esse bloqueio deixa de existir.
- Após redefinir a senha, sessões abertas em todos os dispositivos devem exigir novo login.
- O comportamento de fechamento completo do navegador precisa de homologação em navegador real.

## Validações e erros

- E-mail: obrigatório, formato válido e até 320 caracteres.
- Nome completo: obrigatório em cadastro novo, aparado, entre 2 e 200 caracteres e com pelo menos duas partes não vazias.
- Senha de cadastro e redefinição: de 8 a 128 caracteres, sem composição adicional obrigatória.
- Senha de login: obrigatória e até 128 caracteres.
- E-mail duplicado: erro explícito e compreensível.
- Confirmação e redefinição: `userId` e token obrigatórios.
- Rate limiting continua retornando `429`; falhas comuns de login retornam `401`.
- Erros usam Problem Details e não expõem credenciais, tokens, hashes ou detalhes internos.

## Segurança

- Chave JWT, issuer, audience e connection string são obrigatórios e devem abortar a inicialização quando ausentes ou inválidos.
- O cookie refresh permanece `HttpOnly`, `SameSite=Strict` e `Secure` fora de Development.
- Não bloquear automaticamente a conta aumenta a importância do rate limiting, observabilidade e monitoramento de tentativas.
- Permitir vários dispositivos exige revogação global confiável na redefinição de senha.
- Registros de sessão retidos indefinidamente contêm metadados e IPs; o acesso deve permanecer restrito e protegido.
- Os riscos já identificados sobre CSP com localhost e forwarded headers continuam pendentes de auditoria institucional e não foram decididos nesta revisão.

## Acceptance Criteria

- **Given** um cadastro válido, **When** a conta é criada, **Then** fica pendente de confirmação e não consegue entrar antes de confirmar o e-mail.
- **Given** um novo cadastro sem sobrenome, **When** a solicitação é enviada, **Then** a API recusa a entrada e a interface associa o erro ao campo de nome completo.
- **Given** um cadastro com nome e sobrenome válidos, **When** a conta é criada e associada à organização, **Then** o nome completo permanece disponível como identificação humana do membro.
- **Given** um e-mail já cadastrado, **When** um novo cadastro é tentado, **Then** a resposta informa claramente que o e-mail já está cadastrado.
- **Given** uma senha com 8 caracteres, sem composição especial, **When** o cadastro ou reset é validado, **Then** ela é aceita.
- **Given** várias tentativas incorretas, **When** o limite de lockout anteriormente configurado é ultrapassado, **Then** a conta não fica bloqueada; o rate limiting ainda pode recusar excesso de requisições.
- **Given** configuração obrigatória ausente, **When** a aplicação inicializa, **Then** o startup falha antes de atender requisições.
- **Given** um refresh token válido, **When** duas renovações concorrentes o apresentam, **Then** exatamente uma é aceita e a outra é recusada sem revogar a sessão vencedora.
- **Given** sessões válidas em dois dispositivos, **When** não ocorre reset de senha, **Then** ambas podem permanecer ativas.
- **Given** uma senha redefinida, **When** a operação conclui, **Then** todas as sessões anteriores são recusadas.
- **Given** um navegador autenticado, **When** ele é completamente fechado e reaberto, **Then** o usuário precisa fazer login novamente.
- **Given** um usuário comum já associado à organização A, **When** convite ou cadastro tenta associar o mesmo login/e-mail à organização B, **Then** a segunda associação é recusada sem alteração parcial.
- **Given** a única associação do usuário desativada, **When** tenta acessar APIs funcionais, **Then** o tenant é recusado embora a identidade global continue existente.
- **Given** sessões expiradas ou revogadas, **When** o tempo passa, **Then** seus registros continuam persistidos.
- **Given** dois tenants, **When** dados são consultados ou gravados, **Then** somente o tenant ativo é acessível.

## Test Gate Mapping

### Evidência existente

- Hash e rotação: `PlatformCrossCuttingTests.Refresh_token_is_hashed_rotated_and_reuse_revokes_family`.
- Auditoria e redaction: `PlatformCrossCuttingTests.DbContext_writes_immutable_audit_with_actor_ip_and_redaction`.
- Filtro por organização: `MultitenancyPersistenceTests.GlobalFilters_ReturnOnlyTheActiveOrganization`.
- Escrita cross-tenant: `MultitenancyPersistenceTests.SaveChanges_AutomaticallyStampsNewRootAndRejectsAnotherTenant`.

### Testes obrigatórios ainda ausentes

- Integração HTTP de cadastro, confirmação, login, refresh, logout, recuperação e reset.
- Cadastro exigindo nome e sobrenome, preservação do nome até a associação e rejeição sem escrita parcial.
- Cadastro com e-mail duplicado e mensagem explícita.
- Senha mínima de 8 caracteres sem composição obrigatória.
- Ausência de lockout automático com rate limiting preservado.
- Falha de startup para chave, issuer, audience ou conexão ausentes.
- Concorrência real: uma rotação aceita, duplicata recusada e família vencedora ativa.
- Encerramento da sessão após fechar e reabrir o navegador.
- Sessões simultâneas em dispositivos/contextos distintos.
- Reset de senha invalidando todas as sessões.
- Unicidade de membership por login/e-mail e rejeição atômica de convite para segundo tenant.
- Desativação da única associação preservando a identidade global, mas sem acesso funcional.
- Retenção de registros expirados e revogados.
- Cookies, JWT, headers, CORS, HSTS, middleware organizacional e Problem Details.

## Homologação manual pendente

1. Confirmar cadastro com nome e sobrenome obrigatórios, recebimento do fluxo de confirmação e bloqueio de login antes da confirmação.
2. Confirmar mensagem de e-mail já cadastrado.
3. Confirmar senha de 8 caracteres sem requisitos de composição.
4. Confirmar que tentativas erradas não bloqueiam a conta, sem desativar o rate limiting.
5. Confirmar sessões simultâneas em dois navegadores ou dispositivos.
6. Confirmar que fechar completamente e reabrir o navegador exige novo login.
7. Confirmar que redefinir a senha derruba as sessões abertas.
8. Confirmar que um usuário comum não pode ser associado a uma segunda organização e não vê seletor de tenants.
9. Confirmar que desativar sua única associação preserva a identidade, mas bloqueia todo acesso funcional organizacional.

## Pending Decisions

As decisões levantadas sobre refresh concorrente, retenção, desativação organizacional, membership única, configurações obrigatórias, e-mail duplicado, confirmação, lockout, senha, duração da sessão, múltiplos dispositivos e reset de senha foram encerradas por PO nesta revisão.

A exigência de nome completo no cadastro e o fallback transitório de e-mail para legados também foram encerrados por PO e registrados em D63; a regra visual detalhada vive em `SPEC-RESPONSIBLE-DISPLAY-NAME`.

Continuam fora desta rodada:

1. Remover endpoints localhost da CSP fora de Development.
2. Validar a confiança em forwarded headers na topologia institucional.
3. Definir a aplicação explícita das políticas `InternalUser` e `ConfirmedEmail` em endpoints específicos.

## Risks

- A implementação atual de refresh concorrente cria mais de um sucessor para o mesmo token e diverge do contrato aprovado.
- O armazenamento atual em `localStorage` e o refresh cookie persistente mantêm a sessão após fechar o navegador.
- Remover lockout exige preservar e testar rate limiting para reduzir tentativas automatizadas.
- Retenção indefinida acumula metadados de sessão e IPs, exigindo controle de acesso e capacidade operacional.
- Confiar em forwarded headers é perigoso se a API puder ser acessada sem o proxy isolador.
- CSP com localhost pode ser inadequada para produção.

## Inconsistências documentação-código/teste

- D35 registra lockout por falhas reais; a decisão humana mais recente determina que não haja bloqueio automático. Pela hierarquia de autoridade, o contrato desta revisão prevalece para a futura implementação, mas a ADR deve ser harmonizada antes de alterar o produto.
- A senha atual exige 10 caracteres e composição completa; o contrato aprovado exige somente 8 caracteres.
- `RefreshTokenService` aceita reapresentação dentro de 30 segundos; o contrato aprovado aceita somente uma renovação e recusa a duplicada sem revogar a família.
- `PlatformCrossCuttingTests` espera revogação da família na reutilização imediata, comportamento que também diverge do contrato aprovado.
- Issuer e audience são usados na validação, mas não abortam explicitamente o startup quando vazios.
- A SPA persiste access token em `localStorage` e a API emite refresh cookie com expiração, contrariando o encerramento ao fechar o navegador.
- A mensagem de cadastro duplicado depende da descrição do Identity e não possui teste de contrato.
- `RegisterRequest` e a tela de cadastro não possuem nome completo; o fluxo atual não consegue validar nome e sobrenome nem garantir sua disponibilidade na associação organizacional.

## Rollback

Não aplicável nesta revisão documental. Cada tarefa de implementação deverá definir rollback proporcional, especialmente para mudanças em sessão, cookies e rotação concorrente.

## Traceability

D17/D35/D58/D63 e ROADMAP Fase 7 → SPEC-AUTH-001 (`approved`, revisão humana) → SPEC-RESPONSIBLE-DISPLAY-NAME → gaps de implementação e homologação ainda sem task específica nesta spec.

## Referências precisas

- `DECISIONS.md` — D17, D34 e D35.
- `ROADMAP.md` — Fases 6C e 7.
- `src/Detran.Kanban.Api/Controllers/AuthController.cs` — cadastro, confirmação, login, refresh, logout, recuperação, reset e contratos.
- `src/Detran.Kanban.Api/Program.cs` — Identity, senha, lockout, JWT, rate limiting e validações de startup.
- `src/Detran.Kanban.Api/Services/JwtTokenService.cs` — claims, assinatura e expiração.
- `src/Detran.Kanban.Api/Middleware/OrganizationContextMiddleware.cs` — seleção e validação do tenant.
- `src/Detran.Kanban.Infrastructure/Services/RefreshTokenService.cs` — emissão, hash, rotação, tolerância e revogação.
- `src/Detran.Kanban.Infrastructure/Persistence/AppDbContext.cs` — tokens, filtros, escrita, auditoria e redaction.
- `src/Detran.Kanban.Web/src/services/api.ts` — persistência local do access token e renovação.
- `tests/Detran.Kanban.Tests/PlatformCrossCuttingTests.cs` — evidências atuais de refresh e auditoria.
- `tests/Detran.Kanban.Tests/MultitenancyPersistenceTests.cs` — evidências atuais de isolamento.
