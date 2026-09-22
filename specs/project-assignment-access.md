# SPEC-PROJECT-ASSIGNMENT-ACCESS

**Status:** approved
**Aprovacao humana:** em 2026-09-22 o PO escolheu explicitamente "Exigir acesso ao projeto antes de atribuir a tarefa" para resolver E14.
**Historia:** `stories/project-assignment-access.md`.

## Contrato

1. Atribuicao nao concede acesso ao projeto, quadro ou tarefa automaticamente.
2. Adicionar responsavel principal ou adicional exige que a pessoa tenha acesso ao projeto conforme o resolvedor vigente, na mesma organizacao. Vínculo individual, equipe e autoridade administrativa continuam conforme as regras existentes; nao criar hierarquia paralela.
3. O ator continua precisando da permissao Assign; acesso desativado, outro tenant e negacao explicita continuam recusados.
4. UI oferece somente pessoas elegiveis no projeto para novas atribuicoes; a API revalida para impedir bypass. Erro em portugues orienta conceder acesso ao projeto antes de atribuir.
5. Minha Trabalho nao expoe itens sem acesso ao projeto. Vínculos antigos inconsistentes nao sao apagados automaticamente; ficam para revisao administrativa.
6. Nao ampliar autorizacao de leitura de projetos/quadros como efeito da atribuicao. Sem migration nem exclusao de dados.

## Testes

- Atribuicao a membro com acesso: aceita; sem acesso, outro tenant ou inativo: recusada.
- Regra aplicada a responsavel principal e adicional, inclusive update/create/bulk/automacao quando houver atribuicao.
- Minha Trabalho nao retorna tarefa cuja autorizacao de projeto foi perdida.
- UI mostra mensagem acionavel e preserva os demais responsaveis.
