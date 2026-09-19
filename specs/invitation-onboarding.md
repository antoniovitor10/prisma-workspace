# SPEC-INVITATION-ONBOARDING

**Status:** approved
**Aprovação humana:** PO em 2026-09-19: “pode implementar e apagar o usuario ai eu testo”, seguido de “pode colocar em produlção”. Aprova G-SPEC e G-DEPLOY deste recorte. Sem migration.

## Contrato

- Link existente `?invite=` permanece compatível. Uma consulta POST pelo token retorna somente e-mail, organização e existência de conta para convite válido.
- Cadastro pelo convite solicita nome completo (nome e sobrenome, até 200 caracteres), senha e confirmação idênticas. Regras atuais de composição de senha são preservadas neste recorte.
- E-mail e papel vêm exclusivamente do convite persistido; não aceitar valores de e-mail, tenant ou papel enviados pelo cliente.
- Convite válido comprova posse do e-mail, dispensando o segundo e-mail de confirmação apenas neste fluxo. Cadastro público normal permanece inalterado. Esta exceção especializa a confirmação da SPEC-AUTH-001.
- Conta nova, confirmação, associação e consumo do convite ocorrem atomicamente; concorrência/replay não podem consumir duas vezes. Token continua armazenado só como hash.
- Conta existente exige sua senha atual, nunca troca senha pelo convite. A prova do convite pode confirmar a identidade existente após validação da senha. Preservar papel de associação já existente e não reativar membro desativado por convite antigo.
- Recusar associação a outra organização para usuário comum, organização inativa, token inválido/cancelado/expirado/usado e senha incorreta. Não emitir sessão nessas falhas.
- Após concluir, emitir sessão pelo mecanismo existente, selecionar a organização, limpar convite pendente e abrir a aplicação.
- Erros de cadastro duplicado e senha divergente em português. Não registrar senhas ou tokens.

## Validação

Testar cadastro e entrada pela UI desktop/mobile; conta existente, senha incorreta, confirmação divergente, replay, expiração, cancelamento, organização inativa e concorrência. Preservar cadastro público e suíte E2E.

## Operação

Publicar somente após validação. Excluir exclusivamente a conta de teste `vitor@zdoc.com.br` depois de verificar ausência de dados funcionais e obter backup recuperável; preservar o convite não consumido para teste do PO. Qualquer vínculo funcional impede exclusão automática.
