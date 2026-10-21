---
id: ADR-0095
titulo: "Seleção de provedor e modelo por conversa no chat do assistente de IA"
status: aceita
data: 2026-10-07
origem: "DECISIONS.md D95"
substitui: ""
---

# ADR-0095 · Seleção de provedor e modelo por conversa no chat do assistente de IA

## Decisão

- **D95** — Experiência de conversa inspirada no Zapmind (US-AI-011 / SPEC-AI-004), solicitada pelo PO e aprovada em 2026-10-07 com "Aprovar spec e migration descrita" (G-SPEC/G-MIGRATION). Provedor/modelo escolhidos por conversa entre conexões Chat testadas da instalação; padrão global preservado. Preferência opcional persistida em duas colunas nullable de AiConversations, sem alterar mensagens/tarefas ou copiar contas do Zapmind. Catálogos no servidor, cache curto vinculado ao teste da conexão, validação antes de salvar/enviar, ausência de fallback silencioso, preço desconhecido para modelo sem preço e envelope de leitura para capacidades não testadas. Preservar permissões, mascaramento, cotas e consumo. Painel lateral com expansão e histórico/compositor semelhantes ao Zapmind, identidade D68. Migration primeiro no E2E; produção após checks e backup.
