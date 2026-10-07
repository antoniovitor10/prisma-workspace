# SPEC-AI-003: Gemini por assinatura e login Google na ponte CLI

**Status:** draft
**G-SPEC:** pendente; proposta preparada após o pedido explícito do PO em 2026-10-07.
**Story:** US-AI-010 (`stories/ai-gemini-subscription.md`).
**Fase:** 15, refinamento do assistente IA (D91).
**Relação:** estende a SPEC-AI-002 somente para admitir Gemini/CliSubscription. Preserva os contratos da fundação e dos outros provedores.

## Objective

Adicionar assinatura via Gemini CLI com autenticação Google real, seleção de modelos e interface consistente com Codex/Claude. Preservar Gemini por chave de API como opção independente.

## Scope

- Provedor Gemini passa a oferecer `ApiKey` e `CliSubscription`, este com adaptador `gemini` instalado na ponte.
- Contas pessoais Google, incluindo acesso gratuito e assinaturas Google AI Pro/Ultra quando admitidas pela CLI. A integração não promete detectar plano nem liberar modelo sem teste.
- Login manual oficial em ambiente sem navegador no servidor, código temporário, confirmação, cancelamento e expiração.
- Catálogo da CLI, chat textual e consumo usando o mesmo isolamento já aprovado.
- Sem migration, troca de stack, importação de contas locais ou integração com APIs privadas do Code Assist.
- Contas corporativas que exijam projeto/licença Google Cloud ficam fora desta primeira entrega; mostrar orientação explícita se o provedor recusar esse fluxo.

## Functional Requirements

1. Em Gemini, oferecer `Chave de API` e `Assinatura via Gemini CLI (experimental)`. CLI apresenta `Entrar com Google`, o aviso de possível bloqueio/perda da conta da SPEC-AI-002 e seu checkbox de aceite. Explicar que a conta é usada pela instalação inteira.
2. Usar a conta Google associada à assinatura; não pedir senha, chave de API, token OAuth ou credencial do desenvolvedor. Google AI Pro/Ultra e chave Gemini têm acessos/cotas distintos.
3. Iniciar autenticação oficial da versão fixada `@google/gemini-cli@0.63.0`, com `NO_BROWSER=true`, usando o mecanismo de login Google da própria CLI. É permitido um invólucro mínimo dos módulos oficiais da versão fixada para iniciar/verificar autenticação sem gerar resposta. Não reimplementar troca OAuth nem copiar credenciais OAuth do código upstream.
4. Retornar somente link HTTPS de autenticação em `accounts.google.com`, estado e indicação de código necessário. A CLI oficial gera PKCE e mantém estado/verificador internamente. O administrador abre o link, autoriza no Google e cola o código temporário em campo protegido. Código vai uma única vez para o processo oficial e é descartado após confirmar/cancelar.
5. Sessão vinculada a administrador/adaptador, uma tentativa por adaptador, prazo máximo de cinco minutos, cancelamento e encerramento do processo ao expirar. Consulta/conclusão por outro administrador falha. Não publicar callback local do contêiner na Internet.
6. Login só é confirmado pela autenticação Google validada usando o mecanismo oficial, sem inferência. Arquivo existente, seleção de modelo, processo iniciado ou código aceito no Prisma não contam como sucesso. Falha/expiração mantém conexão inativa e apresenta nova tentativa.
7. Credenciais persistem exclusivamente em novo volume privado Gemini, gravável por `node`, separado de Codex/Claude. Códigos, URLs temporárias, tokens e e-mail da conta não vão para logs, Git, telemetria, notas ou memória. Configuração explícita evita herdar chaves ou Google Cloud do ambiente.
8. Após login, listar opções oficiais da CLI fixada, identificadas como catálogo/aliases da CLI. Derivar nomes e identificadores da versão instalada; não misturar catálogo da API nem inventar modelos. Teste verifica acesso efetivo antes de ativar. Usuário pode procurar nome/identificador como nas outras conexões.
9. Comando de chat usa versão fixada, diretório temporário e resposta JSON oficial. Desabilitar ferramentas, shell, extensões, hooks, MCP, memória, histórico persistente e acesso a projeto/banco. Limitar tempo/saída e encerrar ao cancelar. Ler resposta e estatísticas de uso quando fornecidas; ausência de estatísticas conserva medição estimada da fundação, sem inventar gratuidade.
10. Novo login Google invalida teste/ativação das conexões Gemini/CLI. Preservar as conexões API Gemini, Codex e Claude. Conexões Gemini/CLI históricas ficam editáveis; não autenticar, trocar modelo ou ativar automaticamente.
11. Interface explica estados antes do seletor. Colar chave nos métodos API e clicar no modelo não dispara consulta redundante; esse fluxo segue a correção da SPEC-AI-002.

## Contracts

- Preservar rotas administrativas existentes `/api/admin/ai/models` e `/api/admin/ai/cli/{provider}/...`; admitir `Gemini` no método CLI.
- Ponte admite somente adaptador `gemini` adicional em sua allowlist; dono, token privado, risco, tamanho e duração continuam protegidos.
- Instalação atualiza a imagem da ponte e adiciona somente o volume Gemini, mantendo volumes/contas Codex e Claude, token externo e rede privada sem SQL/portas públicas.
- Operador verifica imagem/saúde/configuração e permite rollback da ponte, sem apagar os volumes ou sobrescrever credenciais. Publicação segue a autorização do PO após os checks; login real exige ação do próprio administrador.

## Acceptance Criteria

- Gemini oferece os dois métodos corretos, sem exigir chave ao escolher assinatura.
- Aviso/aceite e `Entrar com Google` iniciam autenticação oficial; link e código são temporários e protegidos.
- Login confirmado libera catálogo/modelo/teste; recusa, cancelamento, expiração ou outra pessoa não ativam conexão.
- Modelos correspondem à CLI instalada; modelos de outros provedores não entram no catálogo Gemini.
- API Gemini, OpenRouter, Codex e Claude continuam funcionando; trocar destino não encaminha chave salva.
- Nenhuma ferramenta, extensão, hook, MCP, projeto, banco ou histórico persistente é acessível ao processo Gemini.
- Upgrade preserva configuração/volumes anteriores, com saúde e rollback conferidos.
- Fluxos E2E desktop/mobile passam, incluindo Gemini assinatura e correção do seletor API.

## Tests and Gates

- G-SPEC antes de alterar código de produto; nenhuma aprovação pelo agente.
- Backend: allowlist, autoridade, vínculo de credenciais, sessão e invalidação de teste.
- Ponte: argumentos/configuração restritos, isolamento, autenticação real versus arquivo existente, PKCE delegado, URLs permitidas, dono, código, cancelamento e expiração.
- Prova da versão fixada: ajuda/modelos e iniciação/cancelamento do login oficial sem autenticar conta humana nem gerar inferência.
- Frontend: métodos, orientação e catálogo; build `tsc -b`, testes e lint.
- E2E completo em SQL dedicado, autenticação Google falsa/controlada e fluxos API/CLI existentes. Conta humana apenas na homologação pelo PO.

## Data Impact

Sem schema. Volume privado Gemini adicional; sessões pendentes somente em memória. Upgrade não apaga volumes anteriores nem histórico de conversas.

## References

- [Google: autenticação Gemini CLI e contas Pro/Ultra](https://geminicli.com/docs/get-started/authentication/).
- [Google: fluxo oficial manual com PKCE e código](https://github.com/google-gemini/gemini-cli/blob/main/packages/core/src/code_assist/oauth2.ts).
- [Google: seleção de modelos](https://geminicli.com/docs/cli/model/).
- [Google: resposta JSON e estatísticas em modo headless](https://geminicli.com/docs/cli/headless/).
- [Google: configuração, ferramentas e diretório privado](https://geminicli.com/docs/reference/configuration/).

As fontes confirmam a possibilidade do fluxo; a execução na versão fixada ainda deve ser verificada antes da implementação/publicação.
