# SPEC-AI-002: Configuração guiada, catálogo de modelos e autenticação CLI

**Status:** approved
**G-SPEC:** aprovado pelo PO Vitor em 2026-10-06: "pode efetuar e publicar", em resposta à revisão preparada e ao pedido explícito de aprovação da SPEC-AI-002.
**G-DEPLOY:** aprovado pelo mesmo pedido do PO para publicação após os checks.
**Story:** US-AI-009 (`stories/ai-connection-onboarding.md`).
**Fase:** 15, refinamento da fundação IA já publicada (D91 / SPEC-AI-001).

## Objective

Permitir configurar a IA por provedor, forma de conexão e modelo, com autenticação real e sem o formulário técnico extenso apresentado em produção. Aplicar a seleção de modelos a todas as opções suportadas e tornar explícito o risco da assinatura via CLI.

## Context

Em 2026-10-06 o PO pediu modelos correspondentes à escolha do provedor, em todas as opções, autenticação real por CLI, aviso explícito de possível perda da conta e uma interface com poucas opções. A implementação atual permite combinações inválidas, exige identificadores livres e apresenta URL, preços e cotas simultaneamente.

## Scope

- Refinar as Configurações de IA existentes, preservando a identidade D68 e os contratos de chat, permissões, histórico, consumo e cotas da SPEC-AI-001.
- Consultar catálogos dos provedores no servidor, sem chamadas de geração nem cobrança de inferência para listar modelos.
- Autenticar Codex e Claude pelo mecanismo oficial das CLIs instaladas na ponte opcional. Exibir disponibilidade, progresso, expiração, erro e cancelamento.
- Manter a ponte isolada em contêiner, sem banco, projeto, ferramentas ou histórico de conversas. Preparar instalação e publicação compatíveis com essa rede opcional.
- Sem migração, novas tabelas, novas bibliotecas de frontend ou compartilhamento de conversas.

## Functional Requirements

### Configuração principal

1. A ordem do formulário é `Provedor`, `Como conectar`, autenticação quando necessária e `Modelo`. Nome sugerido automaticamente; nome editável, preços e URL opcional ficam em `Opções avançadas`. Endpoint personalizado exige sua URL na área principal.
2. Provedores e métodos oferecidos:

   | Provedor | Métodos disponíveis |
   | --- | --- |
   | OpenAI | Chave de API; assinatura via Codex CLI (experimental) |
   | Anthropic / Claude | Chave de API; assinatura via Claude CLI (experimental) |
   | Gemini | Chave de API |
   | OpenRouter | Login OpenRouter; chave de API |
   | Endpoint personalizado | Endpoint compatível com OpenAI |

   Quando existe somente um método, não exigir outra escolha. CLI sem adaptador disponível mostra o motivo e não simula autenticação. Antigravity não é oferecido como integração operacional.
3. Trocar provedor, método ou endpoint limpa modelo, catálogo, segredo digitado e sessão de login do formulário. Respostas de consultas anteriores não podem substituir a lista nova. A credencial salva só pode ser reutilizada pelo servidor quando conexão, provedor, método e endpoint permanecem os mesmos.
4. O modelo é uma seleção pesquisável por nome e identificador, com estados de carregamento, falta de autenticação, catálogo vazio, erro e tentativa novamente. O identificador utilizado fica visível; não exigir digitação normal de nomes como `deepseek` ou `opus 5.5`.
5. Nome e identificador vêm do catálogo. Catálogo não equivale a acesso garantido: o teste continua obrigatório antes da ativação. Modelos incompatíveis com conversa textual são excluídos quando o provedor publica essa informação.
6. URLs padrão são preenchidas internamente por provedor. Listas de modelos:
   - OpenAI: endpoint oficial `/v1/models`, com chave da conexão; excluir famílias conhecidas destinadas somente a embeddings, imagem, moderação, áudio ou realtime.
   - Anthropic: endpoint oficial `/v1/models`, com chave e versão da API; usar identificador e nome de exibição.
   - Gemini: endpoint nativo `/v1beta/models`, com chave em header; manter modelos que suportam `generateContent` e remover prefixo `models/` para o adaptador de chat.
   - OpenRouter: catálogo `/api/v1/models`; manter saída textual e preencher preços opcionais quando informados, convertidos para milhão de tokens.
   - Endpoint personalizado, inclusive provedor com URL sobrescrita: consultar `/models` relativo à URL compatível. Se não oferecer catálogo, permitir identificador manual explicitamente em `Opções avançadas`, com aviso e teste obrigatório.
   - Codex CLI: consultar `model/list` da CLI instalada, sem inferência. Claude CLI: oferecer aliases oficiais suportados pela versão instalada (Sonnet, Opus e Haiku), identificados como aliases, sem inventar disponibilidade por assinatura. O teste verifica acesso efetivo.
7. Preservar visualização e edição das conexões existentes. Conexões antigas com combinação inválida continuam visíveis para correção; não mudar automaticamente o provedor, modelo ou conta. Salvar uma alteração invalida o teste anterior conforme a fundação.
8. A lista usa ações conforme o estado: autenticar quando necessário, testar, ativar após sucesso e editar. Excluir fica em ações secundárias. Mostrar `Sem autenticação`, `Pronta para testar`, `Teste falhou` ou `Ativa` conforme evidência real.
9. Habilitação da organização continua acessível, com cotas em área expansível. Limites da instalação e consumo ficam em áreas secundárias expansíveis. A ação principal não fica diluída entre vários botões equivalentes.

### Login e aviso CLI

10. Ao selecionar CLI, exibir junto à autenticação:

    > Experimental. Usar uma assinatura pessoal nesta integração, especialmente por várias pessoas, pode violar os termos do provedor. O provedor pode bloquear ou encerrar sua conta e você pode perder o acesso à conta e à assinatura. O Prisma não garante que esse uso seja permitido. Para uso de equipe, prefira chave de API ou uma autorização do provedor que cubra a integração.

    Exigir checkbox `Entendo o risco de bloqueio ou perda da conta e quero continuar` antes de iniciar autenticação. Não apresentar bloqueio como consequência certa nem OAuth como autorização para qualquer uso.
11. Botão explícito `Entrar com ChatGPT` ou `Entrar com Claude`. Nunca pedir senha do provedor no Prisma. Codex utiliza `codex login --device-auth`: mostrar link oficial e código temporário; informar quando a conta exige habilitar login por dispositivo nas configurações oficiais.
12. Claude utiliza `claude auth login --claudeai`: apresentar link oficial produzido pela CLI e, se o fluxo headless exigir, um campo temporário para o código/retorno fornecido pelo provedor. O processo oficial faz a troca e grava sua autenticação; não implementar troca OAuth privada, importar a conta do desenvolvedor nem fabricar sucesso.
13. Login tem prazo curto, é vinculado ao administrador que iniciou e ao adaptador escolhido, admite cancelamento e impede sessões concorrentes que sobrescrevam a mesma conta da ponte. Iniciar não autentica; somente o status oficial da CLI confirma. API e ponte protegem contra consulta/conclusão de sessão de outra pessoa.
14. Credenciais persistem exclusivamente em volumes de autenticação da ponte; códigos e URLs temporários não vão para banco, logs, telemetria, notas ou memória. Respostas administrativas usam `no-store`. URLs de autorização devolvidas são HTTPS em domínios oficiais permitidos. Retornar apenas os campos necessários para o usuário concluir o login.
15. Autenticação bem-sucedida atualiza a tela e libera escolha de modelo/teste. Cancelamento, expiração, indisponibilidade da ponte e autenticação recusada mantêm a conexão inativa e mostram uma próxima ação concreta.
16. A autenticação CLI é da instalação, por adaptador, e não de cada pessoa da organização; deixar isso explícito próximo ao aviso. Não assumir isolamento de conta por conexão quando todas usam o mesmo volume.

## Contracts

- `POST /api/admin/ai/models`: consulta com provedor, tipo, URL opcional, id da conexão e credencial nova opcional; resposta somente com lista normalizada, origem e mensagem/estado, sem segredo. Consultas autenticadas ficam fora de cache compartilhado entre contas.
- `GET /api/admin/ai/cli/{provider}/status`: disponibilidade da ponte, adaptador e estado oficial de autenticação.
- `POST /api/admin/ai/cli/{provider}/login`: exige aceite explícito do risco e cria sessão temporária.
- `GET /api/admin/ai/cli/{provider}/login/{sessionId}`: progresso e instrução oficial limitada ao dono.
- `POST /api/admin/ai/cli/{provider}/login/{sessionId}/complete`: fornece o código temporário somente quando o adaptador o exige.
- `DELETE /api/admin/ai/cli/{provider}/login/{sessionId}`: cancela somente a tentativa pendente do dono.
- Operações acima usam MediatR; controller não implementa autenticação nem regra de negócio.
- Ponte expõe metadados e login somente na rede privada e com bearer externo. Rotas e adaptadores têm allowlist; não aceita executável, argumentos ou caminho arbitrários. Cada chamada de chat usa o adaptador correspondente ao provedor da conexão, em vez de misturar provedores num adaptador global.
- Preservar os endpoints de salvar, OAuth OpenRouter, teste, ativação, organização, limites e consumo.

## Invariants

- Somente `IsPlatformAdministrator` acessa catálogos administrativos e autenticação CLI; perfil de organização não concede esse poder.
- Mudança de destino nunca reaproveita ou encaminha credencial salva a outro provedor/host.
- Catálogo e login não leem dados do workspace nem geram chamadas de chat; teste continua medido como na SPEC-AI-001.
- Nenhum login, lista de modelos ou edição ativa uma conexão automaticamente.
- Processos, saídas e tempo são limitados; processo de login é encerrado ao cancelar/expirar e suas instruções são descartadas.
- Ponte continua sem acesso a SQL Server, projeto ou ferramentas. Deploy preserva ambiente, mounts e redes explicitamente permitidas; nenhuma rede arbitrária é aceita para acomodar a ponte.

## Acceptance Criteria

- Ao escolher cada um dos cinco provedores, aparecem somente métodos correspondentes e uma seleção de modelos; trocar escolha não mantém catálogo ou modelo anterior.
- Gemini não oferece um modelo de Claude e uma CLI inválida salva anteriormente exige correção para ser testada/ativada.
- API keys novas e salvas carregam modelos sem retornar ou enviar o segredo a um destino alterado.
- Endpoint personalizado sem catálogo permite somente a alternativa manual indicada, com teste antes da ativação.
- OAuth OpenRouter oferece login claro, retorno com estado validado e escolha de modelo pelo catálogo.
- CLI exige aviso e aceite, inicia o comando oficial, mostra instruções reais, confirma status oficial e suporta erro, cancelamento e expiração.
- Usuário sem a flag e administrador diferente do dono não acessam sessões de autenticação.
- Falha na consulta, na ponte ou no login nunca resulta em conexão ativa; não há credenciais ou códigos em logs.
- Fluxo completo funciona em desktop e mobile com rótulos, teclado, foco, mensagens acessíveis e sem formulário técnico obrigatório.

## Data Impact

Sem alterações de schema. Catálogos e sessões pendentes são temporários. Volumes de autenticação da ponte são externos e privados. Mudança de credencial/destino conserva o histórico de conversa existente.

## Tests and Gates

- G-SPEC obrigatório antes de código de produto; nenhuma aprovação automática.
- Typecheck/build `tsc -b`, testes frontend de seleção e autenticação, backend de autorização/credenciais/catálogos, testes da ponte com processos falsos e prova de iniciação dos comandos oficiais sem autenticar conta real.
- E2E completo em banco dedicado; cenários desktop/mobile de modelos, OAuth, CLI e regressão do chat. Provedores falsos evitam custo e autenticação de conta humana no teste automático.
- Publicar pelo fluxo autorizado de produção após os checks; a autenticação da conta real exige a ação do próprio administrador na tela e o aceite do risco.

## References

- [OpenAI: autenticação Codex em servidor sem navegador](https://learn.chatgpt.com/docs/auth)
- [Anthropic: autenticação Claude Code](https://code.claude.com/docs/en/authentication)
- [Anthropic: comandos de autenticação](https://code.claude.com/docs/en/cli-reference)
- [Anthropic: aliases de modelos](https://code.claude.com/docs/en/model-config)
- [OpenAI: modelos](https://developers.openai.com/api/reference/resources/models/methods/list)
- [Anthropic: modelos](https://platform.claude.com/docs/en/api/models/list)
- [Google: modelos Gemini](https://ai.google.dev/api/models)
- [OpenRouter: catálogo](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties)
