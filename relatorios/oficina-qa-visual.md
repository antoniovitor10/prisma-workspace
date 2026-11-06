# Oficina — controles da rodada QA, checkpoint

Basea2797a7 mais mergesL06–L08 e commitsC1/C6/Brasília na própria fix/qa-round-1, PR27 emrascunho. Rastreabilidade na históriaqa-responsive-controls e GOALS1c/D99. Não é ENTREGA final deC2,C3 ouC4.

C2:busca encolhe antes do seletor de organização; grade de permissões usa trilhas mínimas zero e coluna única no celular; cabeçalhoWiki empilha título/estado/ações. C3parcial:ordenação/contagem/filtros podem quebrar linha; perfil do convite ocupa coluna única no celular. D20 ainda sem correçãoCSS, a nova fixture força statusAguardando informações para reproduzir o corte. C4parcial:administrador orienta solicitarconvite; portal ausente orienta conferirlink; Novoitem fecha porEscape e devolve foco; projeto/equipe/horas têm rótulos visíveis; títuloWiki identificado; Cancelar fecha formulárioIA mesmo sem conexões. D14ainda em diagnóstico, sem correção de gráfico.

Gates reais frontend:tsc-bPASS,27VitestPASS em6arquivos,lint0erros,buildPASS. ContraprovaC4 restaurouTopbar/TimePanel anteriores:2FAIL previstos (Escape/rótulos);10casos não selecionados pelo filtro. finally restaurou fontes e os2focados passaram; nenhum skip novo foi escrito. Backend não mudou neste checkpoint; gates anteriores C1/C6/Brasília preservados, fullbranch fica para o final.

Baseline nativoC2:5PASS/2FAIL, incluindo setup; D15/D18cortados no celular. O primeirooráculoD04inspecionava só ocentro e não detectava borda encoberta:fortalecido para esquerda/centro/direita com organização longa. BaselineC3/C4:6PASS/21FAIL, incluindo setup.34capturas antes preservadas nas duas larguras1440x900 e390x844. As capturas de diagnóstico foram inspecionadas antes das alterações correspondentes.

PrimeiroafterC2:4PASS/3FAIL. D04não chegou ao oráculo visual:fixture criou nome longo sem slug e a API retornou500 por truncamento deOrganizations.Slug. Corrigida fixture com slug curto próprio e leitura doiddoDTO. Defeito de slug fica registrado separadamente, sem alteração incidental deproduto/schema. D15restava3px de corte:imagem mostrou grade ancestral impondo largura; trilha mínima e contêiner corrigidos. Nova execução/C2contraprova/afterfinal pendentes, sem aumentar prazo ou tolerância.

Dois ajustes de oráculo:checkboxes deConteúdodocartão são nativamente ocultos enquanto details está fechado; o teste agora abre o painel antes de exigir alcançabilidade de todos. D06passava pelo nome deplaceholder, agora exige labelreal associado. D14não renderizou o primeiro gráfico; adicionados controleHTTP/contagem e captura precoce para distinguir fixture/carregamento de acessibilidade, sem retirar a exigência de nome doSVG.

Autorrevisão parcial:nenhum alto/médio confirmado nas mudanças; grades respeitam largura e seletores/campos preservam contratos. Não afirmar correção deD14/D20 nem aprovação doafterC2 ainda. Auto-QA final e E2E afetados pendentes na trava global. C1foi9/9nativo eBrasília5/5; C6API/reativação3/3, agora há roteiro adicional do seletor real e capturas, ainda pendente.

Migration:não. REVISAO e QA oficiais:pendentes. Nenhuma produção/main/mergePR/deploy ou nova aprovação humana.

Atualização 08:50 UTC: C2 concluiu o afetado com7PASS; a contraprova reproduziu3FAIL/4PASS e o verde final voltou a7PASS. Seis after foram abertos e inspecionados. C6 seletor real/reativação também3PASS, seis capturas inspecionadas. Relatos individuais emoficina-qa-2.md e oficina-qa-6.md. C3/C4/D14/D20 seguem pendentes. C1 capturas serão reenquadradas para mostrar os controles depois da rolagem vertical, mantendo asserções geométricas e funcionais.

Fechamento dos recortes C1/C3/C4: relatos próprios oficina-qa-1.md, oficina-qa-3.md e oficina-qa-4.md. C3/C4 conjunto25PASS/2FAIL (D13 mobile), contraprova final D14/D20 3FAIL/2PASS e restauração5PASS. Gates combinados724PASS/33skip backend e173PASS frontend. C5 focal5PASS; captura do grafo será fortalecida para esperar o conteúdo real antes da conclusão visual. E2E completo combinado pendente. Ressalvas visuais estão nos relatos individuais.

Retificação: controles25PASS/2FAIL, não27. Full223PASS/8FAIL/40skips; detalhes dos oito casos e correções em andamento. Nenhum resultado do controller substitui log nativo.

## Resultado completo e correções em curso — 20/10/2026

Native completo223PASS/8FAIL/40skips existentes em23,6min. Não é suíte aprovada. As quatro expectativas herdadas de falha em B07/sobreposição de horas continuam herdadas; não foram convertidas em aprovações funcionais nem introduzidos skips novos.

Oito falhas: criação UI L07 desktop/mobile lia resposta anterior à navegação e o Chromium já descartara seu corpo; manual L08 desktop/mobile esperava título genérico, mas a serialização C5 agora entrega o erro específico; D13 mobile Kanban/Lista abre popover a -30px; diário/remover justificativa mobile não alcança Ajustar porque a grade flex centraliza conteúdo mais largo que seu viewport, deixando os primeiros dias fora da área de rolagem.

Correções propostas: L07 limita respostas às requisições iniciadas a partir da resposta da nova navegação e exige cartão antes e depois do reload; manual L08 exige exatamente O fim deve ser maior que o início, mantendo HTTP400/ProblemDetails/ausência de efeitos; popover ganha linha própria e âncora esquerda no celular, com oráculo explícito do checkbox antes das fotos; grade semanal usa safe center, mantendo centralização quando couber e início alcançável quando transbordar. Este último recorte foi diagnosticado e implementado também no PR30, commit71f8232, aplicado aqui por8c2589e. Novo E2E abre os sete dias sem force. Fontes primárias de CSS e diagnóstico no relatório de estabilidade.

Os gates de frontend e novos nativos estão enfileirados. Backend não mudou após724PASS/33skips. Não se declara correção comprovada desses oito casos antes dos resultados novos. O controller privado antigo não propagava exit nativo1; os números acima foram lidos do log, e futuros workers propagam a falha. Leituras anteriores incorretas de27PASS foram retificadas nos relatosC1/C3/C4 e na FILA; o resultado real era25PASS/2FAIL emD13mobile.
