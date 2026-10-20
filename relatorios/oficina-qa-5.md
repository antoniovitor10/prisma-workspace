# Oficina qa-5 — D99

Base original a2797a7 com merges L06–L08. Implementação C5 desenvolvida na scratch própria work/oficina-qa-c5, incorporada à fix/qa-round-1 por999f57e. Correção HTTP f7fb3a5 incorporada; espera do grafo2eda254. PR27 em rascunho. Specs aprovadas de autenticação/histórico e decisãoD99; sem nova estrutura de auditoria.

Cadastro público exige nome e sobrenome na UI, DTO e comando. Nome é preservado em claim Identity provisória, na transação do usuário, e consumido no primeiro membership como DisplayName, na mesma persistência que retira a claim. Nome explícito e claims alheias preservados; convite também coberto. Escolha reversível registrada em PERGUNTAS para revisão humana. Não cria coluna, migration ou fonte permanente paralela.

Linha do tempo vira sexta aba: criação comprovada, entradas e movimentos reais, incluindo revisitas e autoria persistida; reclassificação na mesma coluna não inventa movimento. GETtimeline aplica acesso existente da tarefa. Primeiro histórico posterior à criação não recebe rótulo inventado de criação. Nomes atuais são explicitamente informados; autoria legada desconhecida permanece neutra. Grafo complementar e retorno explícito preservados. Movimentos saem somente da apresentação do histórico geral; eventos, APIs e dados continuam disponíveis. Entradas de Automações/LeadTime e permanência no tooltip são ocultadas, preservando execução/coleta internas.

## Testes reais

- Backend C5:38PASS antes do último ajuste de serialização, incluindo rollback SQL e primeiro vínculo por convite. Contraprova funcional8FAIL/4controles; restaurado12PASS.
- Frontend C5:17PASS em cinco arquivos, tipos app/E2E, lint e build aprovados. Contraprova Auth/Feed/Graph anteriores4FAIL; finalmente restaurado4PASS.
- Primeiro nativo3PASS/2FAIL: cadastro400 perdia mensagens. WriteProblemAsync serializava ValidationErrorResponse pelo contrato base ProblemDetails, descartando Errors. Ramo tipado corrigido. Nova regressão exige mensagens fullName/password e não expõe senha tentada. Gate afetado10PASS; serializador anterior1FAIL/9PASS; restauração10PASS. Controle500 genérico preservado.
- Nativo isolado após correção5PASS. Nativo combinado com API3120 do código integrado5PASS em36,8s; reforçado para aguardar grafo real e ausência de Carregando antes da fotografia. Nenhum sleep, prazo extra, retry ou skip novo.
- Gates completos combinados:724xUnitPASS/33skips existentes,173VitestPASS/45arquivos, tsc-b --force/lint/builds aprovados. FullE2E da branch está em execução, sem afirmar verde completo neste grupo.

## Autorrevisão e auto-QA

Autorrevisão concluída no recorte sem alto/médio confirmado. Conferir a integração com PR28 no repository de organizações. Nenhum snapshot legado foi reescrito; não se afirma snapshot histórico de nomes que não existiam.

Auto-QA1440x900/390x844: formulário de nome, linha do tempo e grafo real inspecionados; as fotos finais do grafo contêm os nós e o controle de retorno. Trilha pode rolar horizontalmente no celular. Nome no primeiro membership é comprovado por resposta API e SQL, não pela foto de navegação pós-onboarding, que pode estar em transição. Capturas próprias em .runlogs/oficina/screenshots/C5-*. O ensaio não inclui leitor de tela real nem aprovação humana da escolha da claim.

Migration: não. REVISAO e QA oficiais: pendentes. Produção, main, merge de PR e deploy intocados. Detalhes históricos do diagnóstico em oficina-qa-c5.md.
