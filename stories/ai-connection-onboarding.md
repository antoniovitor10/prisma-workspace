# Configuração guiada das conexões de IA

**Status:** active
**Módulo:** ai-assistant
**Spec vinculada:** `specs/ai-connection-onboarding.md` (`draft`, aguarda G-SPEC)
**Origem:** pedidos do PO em 2026-10-06 após testar as configurações em produção.

## US-AI-009: escolher provedor, autenticar e selecionar modelo

**Como** Administrador da instalação
**Quero** configurar a IA com poucas escolhas, selecionar os modelos correspondentes a cada provedor e autenticar de verdade quando escolher login ou CLI
**Para** conectar e testar sem descobrir identificadores técnicos nem montar combinações inválidas.

- Ao mudar o provedor ou a forma de conexão, a lista de modelos acompanha a escolha e dados incompatíveis do formulário são limpos.
- Todas as conexões suportadas têm seleção de modelos; endpoint personalizado admite identificação manual em opções avançadas quando não publica catálogo.
- Login OpenRouter oferece uma ação clara para autenticar; CLI Codex e Claude oferecem autenticação real e estado de login.
- Antes de autenticar por CLI, o usuário lê e aceita explicitamente que o uso pode violar os termos do provedor e causar bloqueio ou encerramento da conta, inclusive perda de acesso à assinatura.
- A configuração principal mostra somente o necessário; nome, preços, URL opcional, cotas e consumo ficam em áreas secundárias claras.
- Salvar, autenticar, testar e ativar são estados distintos; login bem-sucedido não ativa uma conexão automaticamente.
