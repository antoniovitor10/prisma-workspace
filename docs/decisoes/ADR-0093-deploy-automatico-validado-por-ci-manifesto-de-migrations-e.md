---
id: ADR-0093
titulo: "Deploy automático validado por CI, manifesto de migrations e contêiner de rollback"
status: aceita
data: 2026-10-06
origem: "DECISIONS.md D93"
substitui: ""
---

# ADR-0093 · Deploy automático validado por CI, manifesto de migrations e contêiner de rollback

## Decisão

- **D93** — Em 2026-10-06, o PO solicitou publicar as melhorias da main de 2026-10-05 e corrigir o deploy automático. A publicação desta instalação será executada após build, testes frontend, governança e E2E aprovados, usando a mesma imagem identificada pelo commit. Credenciais SSH ficam nos secrets do GitHub; a aplicação preserva configuração externa, banco, chaves e anexos. O deploy verifica saúde, mantém o contêiner anterior para rollback e bloqueia diferenças no manifesto de migrations até aprovação e operação explícitas de G-MIGRATION. Esta autorização não aprova a spec draft de IA.
