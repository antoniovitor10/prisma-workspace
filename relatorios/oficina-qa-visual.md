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
