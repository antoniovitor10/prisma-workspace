# Assistente de IA — configuração da primeira entrega

A SPEC-AI-001 entrega chat de leitura, conexões da instalação, histórico privado, mascaramento, consumo e
cotas. Não cria, altera, move ou exclui tarefas. Escrita com aprovação, editor, atas, embeddings e MCP ficam
nas próximas fatias da D91.

## Autoridade da instalação

O servidor verifica `AspNetUsers.IsPlatformAdministrator` diretamente no Identity. Perfis da organização
não concedem essa autoridade. O primeiro administrador do setup recebe a flag; todas as contas existentes
recebem falso na migration. Não existe endpoint de organização para promovê-las.

Após aplicar a migration aprovada, o operador pode promover explicitamente uma conta pelo identificador:

```powershell
# ConnectionStrings__DefaultConnection deve estar no ambiente externo.
.\scripts\set-platform-administrator.ps1 -UserId "identificador-da-conta"
# Para revogar:
.\scripts\set-platform-administrator.ps1 -UserId "identificador-da-conta" -Revoke
```

Não derive a flag de `OrganizationRole.Administrator`, nem use lista de e-mails privilegiados. A flag
autoriza conexões e consumo global; nunca leitura de conversas de outras pessoas.

## Conectar e habilitar

Em Configurações da organização, a seção Assistente de IA mostra administração global para quem tem a flag,
e configuração da organização para quem tem `AdministerOrganization`.

1. Clique em Adicionar conexão, escolha o provedor e a forma de conexão disponível. Informe a chave ou
   autentique e selecione o modelo no catálogo. Nome sugerido, URL opcional e preços ficam em Opções avançadas.
2. Chave de API aceita OpenAI, Anthropic, Gemini e OpenRouter. Endpoint compatível aceita URL própria,
   como `http://ollama:11434/v1`, e chave opcional. `localhost` significa o host da API, não o navegador.
3. OAuth usa OpenRouter com PKCE, estado de uso único associado ao administrador e validade de dez minutos.
   Configure `FrontendBaseUrl` com a origem pública HTTPS (localhost também é aceito).
4. Teste a conexão e confira latência e suporte a ferramentas. Sem sucesso, ela não pode ser ativada.
5. Ative a conexão. Existe uma conexão global ativa de chat. Alterar credencial ou modelo exige novo teste.
6. Habilite IA na organização. O padrão é desligado. O botão IA aparece nas telas autenticadas dessa organização.

Credenciais são criptografadas com ASP.NET Core Data Protection. Preserve o volume das chaves entre
releases; a API retorna apenas indicação de credencial e seus quatro últimos caracteres.

## Chat e permissões

O painel usa contexto validado da rota e as últimas seis trocas. Consulta busca textual, tarefas, detalhe,
visão geral do projeto, atividade recente e Meu trabalho. Projetos/tarefas negados ou arquivados não entram
no contexto; consultas dentro de um projeto ficam nesse projeto. Resultados têm limites de linhas e
indicam truncamento no resumo. Referências autorizadas viram links internos.

Há streaming e interrupção da chamada; trocar de organização descarta a resposta pendente. Conversas são
privadas por organização e usuário. O dono pode listar, renomear e excluir. Desativar um membro apaga seu
histórico daquela organização, mantendo as medições sem conteúdo de conversa.

Todo texto passa por mascaramento antes de qualquer provedor, inclusive local ou CLI. Dados e resultados
de ferramentas são tratados como dados não confiáveis. Nesta entrega não existem ferramentas de escrita.

## Uso e cotas

Limites diários em tokens são verificados antes de cada chamada: instalação, organização, usuário. Zero
significa sem limite; campos vazios da organização herdam o padrão da instalação. As cotas renovam à
meia-noite do fuso configurado, por padrão `America/Sao_Paulo`.

Toda chamada registra resultado e duração. Tokens e custo são registrados quando informados pelo provedor;
preço ausente não é mostrado como custo zero. A ponte CLI registra tokens zerados, portanto suas chamadas
não consomem uma cota em tokens. Uso global permite agrupar por dia, organização, usuário ou conexão;
administradores da organização veem somente seu consumo. Os painéis nunca expõem mensagens.

Chamadas concorrentes são admitidas sob lock transacional no SQL Server, considerando reservas em andamento.
Medições de chamadas interrompidas/falhas podem não receber tokens do provedor; nesses casos não se inventa
consumo. A cobrança efetiva continua sendo a informada pelo provedor.

## Ponte CLI experimental

O perfil `ai-cli` é opcional e desligado por padrão. A imagem contém Codex e Claude, com versões fixadas.
Configure externamente `PRISMA_AI_BRIDGE_TOKEN`. O provedor da conexão determina o adaptador: OpenAI usa Codex,
Anthropic usa Claude. Cada um mantém autenticação em volume próprio. Não monte credenciais ou configuração
completos do computador, projeto ou banco.
Antigravity não tem adaptador nesta imagem; não é anunciado como suportado operacionalmente.

```bash
docker compose --profile ai-cli build prisma-ai-bridge
docker compose --profile ai-cli up -d prisma-ai-bridge
```

A ponte usa uma rede separada do SQL Server, não publica porta no host, exige token e roda como usuário
sem privilégios. Cada pergunta cria execução independente e elimina o diretório temporário. Claude usa
`--tools ""`, MCP vazio e `--no-session-persistence`; Codex usa execução efêmera, sandbox de leitura, ignora
configuração do usuário e desativa shell, execução, apps, plugins, hooks e agentes. A conexão CLI do Prisma
sempre usa o envelope de ferramentas, executado pelo backend; nenhuma consulta de banco vai para a ponte.

Em Configurações, escolha assinatura via CLI, leia o aviso de possível bloqueio ou encerramento da conta e
aceite o risco antes de clicar Entrar com ChatGPT ou Entrar com Claude. A conta é da instalação, por adaptador,
e será usada pelas organizações habilitadas. OAuth/login não garante que o provedor permita qualquer uso.
Codex mostra um link e código para autorizar no provedor; pode exigir habilitar login por dispositivo nas
configurações de segurança do ChatGPT. Claude mostra seu link e um campo temporário para o código retornado.
Você nunca informa a senha do provedor no Prisma. O processo oficial confirma o login; em seguida escolha o
modelo, salve, teste e ative. Cancelamento e expiração descartam as instruções temporárias.

Para o VPS desta instalação, o operador prepara a fonte em `/home/dev/prisma-deploy/incoming/<commit>/ai-bridge`
e executa `scripts/install-ai-bridge.py <pasta-da-fonte> <commit>`. O script cria rede exclusiva `prisma_ai_bridge`,
volumes privados e token externo em arquivo com permissão 600; não autentica uma conta. O deploy preserva a
rede original e adiciona somente essa rede aprovada após conferir isolamento e saúde da ponte. Não há portas
públicas da ponte. Nenhum token é incluído no Git ou na saída do operador.

Catálogo da CLI e aliases Claude não garantem acesso a um modelo pela assinatura: o teste confirma acesso
efetivo. Validação automatizada usa simuladores e não consome assinatura real. A inicialização dos comandos
oficiais também é verificada em contêiner descartável sem concluir login nem enviar perguntas.

## Migration e publicação

`20261006134545_Add_AiAssistant_Foundation` adiciona seis tabelas de IA e a flag falsa no Identity.
G-SPEC, G-SCOPE da flag e G-MIGRATION foram aprovados pelo PO em 2026-10-06; a aplicação/validação autorizada
é primeiro no E2E dedicado. A aprovação dessa validação não é um registro de publicação em produção.

Para produção: backup verificado, promoção da migration conforme gate operacional e atualização do
manifesto de migrations do deploy. A automação recusa a imagem se o manifesto não corresponder à estrutura
autorizada. Consulte [deploy](deployment.md). Não contorne essa comparação para publicar o novo schema.

Rollback funcional: `Ai__Enabled=false` esconde o botão e bloqueia endpoints de IA; desativar a conexão
cancela chamadas em andamento. Reverter a imagem preserva as tabelas/flag sem uso. Não execute `Down` em
produção como rollback de código, pois isso removeria históricos e medições.

## Referências dos adaptadores

- [Chat Completions streaming — OpenAI](https://developers.openai.com/api/reference/resources/chat/subresources/completions/streaming-events).
- [Compatibilidade OpenAI — Gemini](https://ai.google.dev/gemini-api/docs/openai).
- [Messages — Anthropic](https://platform.claude.com/docs/en/api/messages/create).
- [OAuth PKCE — OpenRouter](https://openrouter.ai/docs/guides/overview/auth/oauth).
- [Configuração — Codex](https://developers.openai.com/codex/config-reference) e [CLI — Claude](https://code.claude.com/docs/en/cli-reference).
