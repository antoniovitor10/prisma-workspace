# Validacao parcial no navegador - 22/09/2026

Ambiente: VPS, worktree `/home/dev/prisma-invite-hotfix-20260922`, imagem `prisma-invite-hotfix:20260922`, container `prisma-invite-qa-20260922`, banco exclusivo `Prisma_QA_20260922`. Porta loopback 55400 via SSH. Sem SMTP. Nenhum deploy em producao nesta rodada.

## Executado pelo agent-browser

- Criacao do primeiro administrador e organizacao pelo formulario `/setup`: passou.
- Login com a conta criada: passou.
- Criacao de projeto sem datas: passou.
- Backlog inicialmente com filtros recolhidos: passou.
- Criacao de tarefa pelo backlog: passou.
- Edicao da descricao e persistencia apos fechar/reabrir: passou; captura visual realizada.
- Kanban direto do projeto e movimento por botao para Em andamento: passou.
- Convite invalido com API real: recuperacao por Ir para o login e login posterior passaram.
- Convite de membro: link gerado; aviso de SMTP indisponivel mostrado.
- Cadastro do convidado em sessao separada com nome, senha e confirmacao: passou, login automatico sem confirmacao adicional.
- Adicao de dois responsaveis na mesma tarefa: passou, interface mostra 2 responsaveis.
- Segunda conta ve tarefa em Meu trabalho e recebe notificacao: passou.
- Segunda conta clica Kanban em Meu trabalho: FALHOU; Nenhum quadro disponivel e zero tarefas. Reproducao do E14. Pessoa e membro da organizacao e responsavel, mas nao foi adicionada separadamente ao projeto. Investigar contrato de acesso antes de ampliar permissoes.

## Ressalvas

Uma primeira atribuicao seguida imediatamente por fechar o modal nao apareceu na reabertura. Nova tentativa aguardando atualizacao para 1 responsavel persistiu. Investigar possivel concorrencia/autosave; nao classificado como causa confirmada.

Build frontend e .NET passaram na imagem. Quatro E2Es focados de convite invalido passaram em desktop/mobile. Suite E2E completa ainda pendente. Colunas independentes, demais casos do PDF, relatorios, anexos/imagens, permissoes e regressao mobile ainda nao homologados.

Worktree amplo `/home/dev/prisma-qa-20260922` contem migracao de colunas ainda sem integracao de aplicacao e quatro ajustes de layout do subagente Luna, ainda nao validados nem publicados. Nao misturar com hotfix sem revisao.

Proximo passo: reproduzir E14 com membro do projeto, revisar autorizacao/listagem de quadros e testar persistencia de responsaveis ao fechar rapidamente; depois completar matriz PDF e suite E2E.
