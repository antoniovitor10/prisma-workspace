---
id: ADR-0039
titulo: "Testes E2E com Playwright e autenticação por setup project"
status: aceita
data: 2026-08-19
origem: "DECISIONS.md D39"
substitui: ""
---

# ADR-0039 · Testes E2E com Playwright e autenticação por setup project

## Decisão

- **D39** — Testes E2E com **Playwright** (@playwright/test). Chromium em cada PR; Firefox/WebKit
  em execucao noturna ou pre-release. Autenticacao via setup project que faz login pela API e
  persiste storageState. baseURL configuravel por variavel de ambiente. Trace on-first-retry,
  screenshot e video em falha, relatorio HTML. Sem sleeps fixos; locators por role, label ou
  data-testid estavel. Nunca executar contra producao. (2026-08-19)
