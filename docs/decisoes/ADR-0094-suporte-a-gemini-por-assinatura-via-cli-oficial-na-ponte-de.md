---
id: ADR-0094
titulo: "Suporte a Gemini por assinatura via CLI oficial na ponte de IA"
status: aceita
data: 2026-10-07
origem: "DECISIONS.md D94"
substitui: ""
---

# ADR-0094 · Suporte a Gemini por assinatura via CLI oficial na ponte de IA

## Decisão

- **D94** — Gemini por assinatura na ponte IA, solicitado e aprovado pelo PO em 2026-10-07 (US-AI-010 / SPEC-AI-003; resposta: "Sim, aprovar e implementar"). Estende D91 e SPEC-AI-002 para admitir Gemini/CLI ao lado de Codex e Claude, mantendo Gemini por chave de API. Usar `@google/gemini-cli@0.63.0` e seus módulos oficiais fixados, com invólucro mínimo para login Google/validação sem inferência; não reimplementar OAuth nem consumir diretamente APIs privadas do Code Assist. Modelos vêm da CLI instalada, acesso exige teste, login exige risco/aceite e conta é da instalação. Volume Google privado separado, ponte sem ferramentas/MCP/hooks/extensões/projeto/banco/histórico persistente, upgrade preserva contas e rollback. Contas pessoais, inclusive Pro/Ultra conforme acesso concedido pelo provedor; contas corporativas com projeto/licença extra fora desta entrega. Sem migration ou troca de stack.
