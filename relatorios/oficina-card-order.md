# Oficina — TASK210: nova tarefa no topo do Kanban

Base: origin/main a2797a7. Branch fix/kanban-new-card-top. Sem migration.

## Alteração

A criação no Kanban calculava mínimo menos100, mas limitava o resultado a zero. Com primeira posição zero, duas criações empatavam e o desempate por ID podia colocar a nova tarefa depois da anterior. positionBeforeColumn agora aceita posições negativas e mantém as demais posições. O validador de criação aceita números finitos estritamente entre ±999999999999, com erro em pt-BR; movimentos já aceitavam posições negativas no armazenamento existente.

Contrato: SPEC-KANBAN-VISUAL-ORDER aprovada, D60, US-KANBAN-VISUAL-ORDER-003. Este recorte encerra o defeito TASK210, não toda TASK018. Não altera ordenação por data, importações, outras entradas de criação, autorização ou schema. A escolha reversível foi registrada em PERGUNTAS-oficina.md.

## Testes reais

- Backend afetado:12 aprovados inicialmente; nove casos do validador no fechamento. Contraprova da validação anterior:2 falhas e7 controles aprovados; restaurado:9 aprovados.
- Frontend afetado:6 aprovados; contraprova do limite zero:1 falha e5 controles aprovados; restaurado:6 aprovados.
- Build backend, tsc -b, tipos explícitos do E2E, lint e build frontend aprovados.
- Suíte xUnit completa:699 aprovados,33 skips preexistentes,0 falhas,57s.
- Vitest completo:164 aprovados em41 arquivos,0 falhas,186,90s.
- E2E novo nas duas larguras:pendente. Teste preserva cartões existentes, cria duas vezes, exige posições -100/-200 e primeiro cartão após recarregar.

## Autorrevisão

Sem achados altos/médios neste recorte. A ordenação permanece estável por ID para empates; criação concorrente ainda pode empatar, como antes. Não se afirma serialização de criações. Limite numérico continua existindo; normalização futura é outro recorte da TASK018. A API preserva posições explícitas, sem reordenar todo o quadro.

## Auto-QA

Unitários e integração acima aprovados; roteiro visual e E2E ainda pendentes em1440x900 e390x844. API3126/Vite3127 e banco exclusivo de QA. Sem produção.

REVISAO e QA oficiais: pendentes.

## Fechamento local — 20/10/2026

Código de produto a09057a. Focal nativo3PASS: cria duas tarefas no topo com posições -100/-200, recarrega, verifica primeira tarefa e preserva posições das demais. Suíte E2E completa121PASS/40skips preexistentes/0FAIL,10,5min, um worker e nenhum retry, sob travas global/CPU. O provedor simulado privado usa18747 e token configurado; nenhum serviço de produção. Tipos refeitos com tsc -b --force: aprovado.

Duas capturas1440x900/390x844 abertas e examinadas: a segunda tarefa criada está no topo e a primeira vem depois, após reload. No celular as demais colunas são acessadas por rolagem horizontal existente; a barra de filtros ainda tem corte no baseline desta branch. C2/C3 corrigem esse problema separadamente no PR27, sem incorporá-lo incidentalmente nesta task.

Autorrevisão e auto-QA do recorte concluídos, sem achado alto/médio confirmado. Concorrência ainda pode produzir empate de posição e normalização continua fora deste recorte; desempate estável porID existente foi preservado. Não declara TASK018 inteira resolvida nem muda importações/criação global. Migration: não. REVISAO e QA oficiais: pendentes. PR33 permanece em rascunho.
