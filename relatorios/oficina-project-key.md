# Oficina — chave técnica fora das telas operacionais, checkpoint

Branch `fix/project-key-hidden`, base exata `origin/main@a2797a7`. Escopo: TASK-024, D56 e SPEC-PROJECT-KEY-AUTO-GENERATION aprovada. História de rastreabilidade criada, sem alterar a spec.

## Mudanças

- Criação gera a chave exclusivamente no servidor; Key legado continua aceito no contrato, mas seu valor e sua validação funcional são ignorados. Chaves existentes e índice permanecem.
- Sidebar, cabeçalho, listas/consulta de itens, detalhe/subtarefas/vínculos, autocomplete, equipes, solicitações, início, Meu Trabalho, sprint e relatórios usam nome do projeto e número/título da tarefa, sem prefixo técnico.
- Catálogo e resultado visual do construtor ocultam projectKey sem mutar os dados recebidos. Relatório só com campo técnico orienta selecionar campo funcional.
- Dados/contratos de API, busca técnica por chave, definições salvas antigas, exportação técnica legada e slugs publicados do portal preservados. O recorte reversível de compatibilidade foi registrado em PERGUNTAS-oficina.md para conferência humana.

## Testes reais até o checkpoint

- Vitest afetados:18/18PASS. Contraprova restaurando cinco componentes antigos:7FAIL esperadas/11PASS, por exposição da chave; fontes corrigidas restauradas.
- Full Vitest após a complementação do construtor:163PASS/41arquivos/0falhas,258s.
- Build frontend (`npm run build`, inclui `tsc -b`)PASS; lintPASS; tipos explícitos do novo E2EPASS. O primeiro tsc-b identificou dois símbolos sem uso, removidos antes da rodada verde.
- Backend afetado inicial13/13PASS; verde final ProjectFeature+Scrum18/18PASS após contraprova2FAIL reais (Key legado aceito pelo código anterior). Fontes e binários corrigidos restaurados. FullxUnit691PASS/33skips preexistentes/0FAIL,68s. Build dos cinco projetos passou com três avisos anteriores. Testes verificam chave legada ignorada, colisão sequencial gerada, validação e preservação de chave existente. Um teste antigo que exigia chave do cliente foi substituído pelo comportamento aprovado; o fake passou a armazenar chaves reais, evitando uma resposta sempre true que provocaria loop.
- E2E novo preparado para a jornada em1440x900 e390x844, screenshot por rota, API sem chave escolhida pelo cliente e preservação do valor interno. Browser ainda aguardando mutex compartilhado; runtime próprio API3123/Vite3125 saudável com bancoPrisma_BoardColumns_Oficina_Key_Seeded noSQLQA3111. Viteusa cache próprio ignorado para não disputar otimizador com organizações.

## Limites e riscos

Não verificados neste checkpoint: suíte completa E2E, auto-QA nas duas larguras e inspeção de capturas. A geração existente resolve colisões sequenciais; não se alega nova serialização da criação concorrente de chaves nem atomicidade adicional entre projeto/quadro.

Autorrevisão parcial: chave permanece nos dados técnicos por compatibilidade, não no rótulo operacional; testes preservam busca técnica e payloads recebidos. API não altera chave de projeto existente. Auto-QA pendente.

Migration: não. Nenhuma entidade, índice, schema, ambiente protegido ou produção alterados. REVISAO e QA oficiais: pendentes. Não é ENTREGA concluída.

Atualização 08:58 UTC: primeiro focal nativo1PASS/2FAIL encontrou uma exposição real no painel Projetos vigentes de Meu trabalho. O teste de componente anterior tinha projects vazio e não cobria esse painel. Corrigido somente o rótulo e adicionada fixture preenchida. Novo afetado2PASS; restaurar a versão1084317 reproduziu1FAIL (outro caso fora do filtro); finally restaurou e2PASS. Tipos,lint,build e novo fullVitest164PASS/41arquivos. Asserção nativa body.includes(key) foi preservada; rerun e fullE2E pendentes. Backend permanece idêntico ao validado691/33skips.
