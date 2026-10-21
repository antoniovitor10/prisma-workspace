---
id: ADR-0067
titulo: "Fluxo de desenvolvimento AI-Native orientado por histórias e especificações"
status: substituida # substituida por ADR-0103
data: 2026-08-26
origem: "DECISIONS.md D67"
substitui: ""
substituida_por: "ADR-0103"
---

# ADR-0067 · Fluxo de desenvolvimento AI-Native orientado por histórias e especificações

## Decisão

- **D67** — O desenvolvimento AI-Native passa a usar o fluxo **História de usuário → Especificação gerada pela IA →
  G-SPEC → Tarefas → Contexto → Implementação → Testes → Homologação da história**. A história registra primeiro a
  necessidade e o comportamento esperado; a spec transforma esse material em contrato funcional e técnico, continuando
  a ser a única unidade aprovada formalmente pelo `G-SPEC`. Histórias não recebem gate nem aprovação individual: na
  homologação ficam como `Ainda não testada`, `Está conforme`, `Precisa de ajuste`, `Não implementada` ou `Bloqueada`.
  Toda observação salva, marcação `Precisa de ajuste` ou `Não implementada` cria ou atualiza uma tarefa rastreável para
  análise da IA, vinculada à história e à spec. A IA classifica se há defeito, ausência de implementação, mudança da
  própria história/spec ou impedimento de teste, sem duplicar tarefa ativa. Mudança de requisito volta para a história e
  exige nova versão da spec e novo `G-SPEC` antes de código; defeito ou ausência cobertos pela spec voltam para a fila de
  implementação. Após a correção, a tarefa é concluída e arquivada, a observação sai da área ativa preservando histórico
  e a história volta para nova homologação. (2026-08-26)
