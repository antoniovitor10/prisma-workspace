# Rastreador local

Usado quando `rastreador: local` em `config.yaml`. Os status ficam no **frontmatter** de cada task em `docs/trabalho/`, versionados junto com o código. É o modo para quem trabalha sozinho ou ainda não tem o Prisma.

## Como o agente lê e muda o status

- **Ler:** abra o arquivo da task e veja o campo `status:` do frontmatter.
- **Listar:** `node metodo/nucleo/scripts/tasks.mjs listar` (ou `listar em-desenvolvimento`). `proxima` mostra as autorizadas com dependências concluídas.
- **Mudar:** edite o campo `status:` e acrescente uma linha na seção `## Histórico` da task:
  `- 2026-10-12 · em-desenvolvimento → em-revisao · agente · <motivo curto>`
- **Comentar:** escreva na seção `## Execução` (relatório), `## Bloqueios` (dúvidas) ou `## Handoff` (sessão interrompida).
- **Antes de mudar:** siga as regras de "Quem move o status" em `metodo/nucleo/fluxo.md`.

## Como o desenvolvedor autoriza

Ele edita o `status:` da task, ou pede explicitamente: "move a T-0007 para em-desenvolvimento". No segundo caso, o agente faz a mudança e registra "desenvolvedor" como autor no histórico. "Executa a T-0007" **não** é um pedido de mudança de status.

## Revisão humana (métrica)

Quando o desenvolvedor tira a task de `em-revisao` (para `em-teste`, `concluida` ou de volta para `em-desenvolvimento`) e o campo `revisao_humana` está vazio, pergunte numa linha e registre a resposta:

> Como foi a revisão da T-0007? (a) **aprovado**: entrou como veio · (b) **ajustes**: pediu mudanças pequenas · (c) **refeito**: boa parte precisou ser refeita

Na saída de `em-teste`, pergunte do mesmo jeito por `homologacao` (`ok` · `falhou` · `nao-se-aplica`). Esses dois campos são o único sinal de que o agente errou sem perceber: nunca preencha por conta própria.

## Limitação conhecida

O status muda **na branch da task**. Até o merge, a branch principal continua mostrando o status antigo. Para quem trabalha sozinho isso é aceitável; para equipe, é o motivo de existir o modo prisma.

## Índice

`node metodo/nucleo/scripts/tasks.mjs indice` gera `docs/trabalho/INDICE.md`, um quadro com todas as HUs e tasks por status. Rode depois de criar tasks ou mudar status. O índice é gerado: nunca edite à mão.

## Validação

`node metodo/nucleo/scripts/tasks.mjs validar` confere campos obrigatórios, status válidos, IDs duplicados, dependências, skills listadas e cobertura de CA. Rode antes de fechar uma sessão de planejamento.
