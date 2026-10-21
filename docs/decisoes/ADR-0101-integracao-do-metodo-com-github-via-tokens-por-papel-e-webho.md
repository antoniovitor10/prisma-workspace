---
id: ADR-0101
titulo: "Integração do método com GitHub via tokens por papel e webhook (fases 2 e 3)"
status: aceita
data: 2026-10-16
origem: "DECISIONS.md D101"
substitui: ""
---

# ADR-0101 · Integração do método com GitHub via tokens por papel e webhook (fases 2 e 3)

## Decisão

- **D101** — Integração do método com o Git (fases 2 e 3 do rastreador Prisma), decisões do PO em 2026-10-16. (1) Provedor: **GitHub**, inclusive no repositório da equipe do DETRAN; GitLab fica fora do primeiro ciclo, com o provedor desenhado para ser plugável. (2) Credencial: **GitHub App** instalada por repositório, com permissão mínima (leitura de PRs, reviews e deployments; escrita de checks/status só na fase 3) e identidade própria de sistema; segredos fora do Git. (3) Primeiro corte (fase 2a): **papéis na API** (tokens humano, agente e sistema, com escopo de projeto, validade e revogação; a recusa de o agente mover `backlog`/`pronta` para `em-desenvolvimento` passa a valer na API para qualquer canal) **e webhook** (cadastro do repositório no projeto, assinatura HMAC, idempotência pelo ID da entrega, identificação por vínculo explícito > branch > `[T-XXXX]` no título, transições de PR da tabela do contrato). Em seguida, a fase 2b (reconciliação periódica) e depois a fase 3 (status `prisma/autorizacao` e deploy observado). (4) **Compatibilidade com a D91:** o MCP do método é uma integração externa com contrato próprio e escreve direto no Prisma o que a matriz do papel do token permite; o assistente de IA interno continua como a D91 define, com escritas como sugestões que exigem aprovação humana. Uma decisão não amplia a outra. Contrato: `specs/metodo-git-integration.md`, que precisa de aprovação do PO (G-SPEC) e da migration (G-MIGRATION) antes do código.
