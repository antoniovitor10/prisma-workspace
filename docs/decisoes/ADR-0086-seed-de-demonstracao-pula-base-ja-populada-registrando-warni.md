---
id: ADR-0086
titulo: "Seed de demonstração pula base já populada registrando Warning"
status: aceita
data: 2026-09-09
origem: "DECISIONS.md D86"
substitui: ""
---

# ADR-0086 · Seed de demonstração pula base já populada registrando Warning

## Decisão

- **D86** — O seed de demonstração **pula silenciosamente não**: pula com aviso. Quando `Seed:DemoEnabled`
  está ligado e a instalação já possui dados, `DbInitializer.SeedDataAsync` retorna `false` em vez de lançar
  exceção, e `Program.cs` registra um `Warning` explicando que nada foi semeado e que é preciso recriar o banco
  para semear do zero. A regra de segurança original permanece intacta — **o seed nunca altera dado
  existente** —, mas deixa de derrubar a aplicação no boot, o que era pior operacionalmente: um restart de
  container contra base populada quebrava a subida. A alteração entrou de carona na correção do
  `WorkflowMoveGuard` (`97a5adf`) sem registro; esta decisão a torna explícita e acrescenta o aviso, para que o
  pulo não passe despercebido por quem esperava um banco semeado. (2026-09-09)
