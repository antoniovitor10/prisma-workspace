---
id: ADR-0053
titulo: "Regras de responsável obrigatório, subtarefas na conclusão e lixeira de 7 dias"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D53"
substitui: ""
---

# ADR-0053 · Regras de responsável obrigatório, subtarefas na conclusão e lixeira de 7 dias

## Decisão

- **D53** — Na gestão de tarefas, criação exige título, projeto, responsável principal, quadro e coluna; a tarefa nunca
  fica sem responsável e removê-lo exige substituto. Participantes autorizados podem editar, mover, concluir e
  apontar horas; excluir, arquivar e restaurar ficam restritos ao responsável principal e administradores. Pai
  só conclui quando todas as subtarefas estiverem concluídas, com ação atômica para concluir todas e então o pai,
  mas seu status é independente nos demais casos. `Excluir` envia à lixeira por 7 dias e é diferente de
  `Arquivar`; ao excluir pai, o usuário escolhe incluir subtarefas ou preservá-las, reatribuindo-as à avó quando
  existir e tornando-as independentes caso contrário. Restaurar família excluída junta restaura toda a família.
  Arquivadas aparecem em filtro e podem ser restauradas. Edição concorrente usa `last write wins`. Esta decisão
  sucede a equivalência entre exclusão e arquivamento da D21. (2026-08-24)
