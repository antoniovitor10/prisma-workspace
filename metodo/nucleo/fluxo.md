# Fluxo de trabalho

Vale para qualquer pedido, de "trocar um texto" a "criar um sistema". O caminho é o mesmo; o que muda é o **tamanho** dos documentos. Em todo passo o agente **propõe** (perguntas com opções, HU, tasks, regras) e o desenvolvedor **decide**.

```
pedido
  └─► ENTREVISTA ──► HU ──► CLARIFICAR ──► (PRD, se grande) ──► TASKS ──► ANALISAR     ← sessão de planejamento
                                                                              │
                                     desenvolvedor autoriza (status) ◄────────┘
                                                   │
  "executa a T-0007" ──► checa · confirma ──► EXECUÇÃO com papéis ──► VERIFICAR ──► entrega   ← sessão de execução
                                                                                      │
                                                 aprovação conforme a autonomia ──► PR/MR ──► produção
                                                                                      │
                                                        última task da HU ──► FECHAR A HU (convergir + retrospectiva)
```

Planejar e executar são **sessões separadas**, com contexto novo. A task carrega todo o contexto entre elas.

## Trilhas por tamanho

O agente classifica o pedido na primeira resposta e diz qual trilha vai seguir. O desenvolvedor pode mudar.

| Trilha | Quando | Artefatos | Skills |
|---|---|---|---|
| **Micro** | ajuste sem regra de negócio, até ~1h: texto, CSS, versão de plugin | 1 task direta (`hu: avulsa`), com critério de pronto | `entrevistar` (1 rodada) → `executar-task` |
| **Bug** | algo que deveria funcionar e não funciona | 1 task com seção "Diagnóstico" | `depurar-bug` → `executar-task` |
| **Funcionalidade** | algo novo ou uma mudança de comportamento | HU + 1 a 4 tasks | `entrevistar` → `escrever-hu` → `clarificar` → `quebrar-tasks` → `analisar-consistencia` |
| **Épico** | mais de uma área, dados novos, integração, risco alto | HU + PRD + tasks + decisões (ADR) | trilha de funcionalidade + `escrever-prd` + `registrar-decisao` |
| **Incidente** | produção quebrada agora | task de hotfix + task de correção definitiva depois | `hotfix-producao` |

Na dúvida entre duas trilhas, escolha a menor e diga por quê. O maior risco do método é virar burocracia em mudança pequena.

## Passos

### 1. Entrevista (`entrevistar`)

- O agente **lê o projeto antes** de perguntar: skills do projeto, `contexto.md`, `principios.md` e o código relacionado.
- O que o código responde, ele **confirma**. O que só o desenvolvedor sabe, ele **pergunta**, com opções específicas do projeto e uma recomendada. Se a ferramenta tiver perguntas interativas, usa.
- **Pergunta muito e em rodadas que nascem das respostas**: no mínimo 3 rodadas numa funcionalidade (desenho → consequências → riscos e aceite), mais se ainda houver dúvida que mude a implementação. Suposição escondida é o erro que mais custa.
- Termina com um resumo do escopo e **"Fecha?"**. Sem "sim", não escreve a HU.

### 2. HU (`escrever-hu`)

- Toda funcionalidade vira uma HU com critérios de aceite numerados (`CA-1`, `CA-2`…) no formato Dado / Quando / Então.
- O agente recomenda **com** ou **sem** PRD. O desenvolvedor decide.

### 3. Clarificar (`clarificar`), portão

- Depois da HU, até 5 perguntas sobre o que ainda está ambíguo (dados, permissões, erros, limites, LGPD). As respostas entram na HU, na seção "Esclarecimentos".
- Sem isso, não se quebra em tasks.

### 4. PRD (`escrever-prd`, papel arquiteto), só na trilha épico

- Descrição completa: problema, objetivo, requisitos, fora do escopo, abordagem técnica, riscos e decisões. Decisões de arquitetura viram ADR (`registrar-decisao`).

### 5. Tasks (`quebrar-tasks`, papel planejador)

- O agente **propõe a lista** (ordem, dependências, risco, complexidade) antes de criar arquivos. O desenvolvedor ajusta.
- **1 task = 1 PR revisável.** Cada task é **autossuficiente**: um agente sem a conversa executa lendo só a task e as skills listadas. Cada task diz quais `CA` cobre.
- Complexidade de 1 a 7; se a estimativa passar de 7, quebra de novo antes de propor.
- Toda task nasce `backlog`.

### 6. Analisar consistência (`analisar-consistencia`), portão

- Só leitura. Cruza HU × PRD × tasks × `principios.md`: todo CA tem task? Alguma task sem CA? Algo fere um princípio? Relatório com severidade.
- Problemas voltam para a etapa dona deles. Só então o agente pede autorização.

### 7. Autorização (pelo desenvolvedor)

- O desenvolvedor move a task para um status de `status.agente_pode_iniciar` (`config.yaml`). Por padrão, `em-desenvolvimento`.
- O agente **nunca** inicia uma task fora desses status, mesmo que peçam.

### 8. Execução (`executar-task`)

- Identifica a task, **checa** (status, dependências, skills, git, DoR) e **confirma** numa única mensagem.
- Papéis em sequência, cada um com contexto próprio quando a ferramenta permite: implementador → testador → revisor de código → revisor de segurança (quando se aplica). O testador escreve os testes depois da implementação. Limite de 2 voltas por task.
- Ambiguidade que muda o resultado: **para**, marca `bloqueada`, registra a dúvida na task e pergunta.
- Sessão encerrada com a task incompleta: preenche a seção "Handoff" (`passar-bastao`).

### 9. Verificar antes de concluir (`verificar-antes-de-concluir`), portão

- Nenhuma task é dada como pronta sem a **saída real** dos comandos de teste, lint e build no relatório, e sem cada `CA` conferido um a um. "Deve funcionar" não é evidência.
- Nenhum segredo no diff: `node metodo/nucleo/scripts/segredos.mjs` limpo (ou falso positivo explicado). Regras de segredo, dados pessoais e conteúdo externo em `metodo/nucleo/referencias/seguranca.md`.
- Todo `CA` com interface tem um **teste de interação** (simula clique, toque ou digitação), escrito pelo testador depois da implementação e provado: quebrado de propósito, ele falha. Detalhes em `metodo/nucleo/referencias/testes.md`.

### 10. Entrega e autonomia

Definida em `config.yaml` (`autonomia`). Uma task com `risco: alto` usa `autonomia_risco_alto`.

| Nível | Nome | O agente vai até… | O desenvolvedor… |
|---|---|---|---|
| 0 | supervisionado | commit na branch da task, sem push | revisa antes do teste e antes do merge |
| 1 | aprovar-merge | push e PR/MR aberto com o relatório | aprova e faz o merge |
| 2 | autônomo | merge (e deploy, se configurado) com todas as verificações verdes; status `em-teste` se `tem_homologacao: true`, senão `concluida` | acompanha |

Os comandos exatos de cada nível estão na skill `executar-task`.

### 11. Fechar a HU (`fechar-hu`)

Quando a última task da HU conclui: **convergir** (o que está no código bate com a HU? faltou algo? só acrescenta tasks, nunca edita código) e **retrospectiva** (o que travou, o que a entrevista não pegou, que regra nova o projeto ganhou). Regras novas viram propostas para as skills do projeto; o desenvolvedor aprova.

## Status das tasks

```
backlog ─► (pronta) ─► em-desenvolvimento ─► em-revisao ─► em-teste ─► concluida
                              │      ▲
                              ▼      │
                            bloqueada
```

| Status | Significado | Quem move |
|---|---|---|
| `backlog` | criada | planejador (ao criar) |
| `pronta` | **opcional**: revisada, esperando a vez. O desenvolvedor pode pular direto para `em-desenvolvimento` | desenvolvedor |
| `em-desenvolvimento` | **autorizada**: o agente pode executar | desenvolvedor |
| `em-revisao` | agente terminou; aguarda revisão humana ou de PR | agente (modo local, e modo prisma no nível 0) · webhook do PR (modo prisma) |
| `em-teste` | aprovada na revisão; em teste/homologação | desenvolvedor · webhook do merge (modo prisma) · agente só no nível 2 |
| `concluida` | em produção ou aceita | desenvolvedor · webhook do deploy (modo prisma) · agente só no nível 2 |
| `bloqueada` | parada por dúvida ou dependência | agente ou desenvolvedor |

### Quem move o status

1. **Por conta própria**, o agente só move para os status em `status.agente_pode_mover_para`. No nível de autonomia 2, também para os de `status.agente_pode_mover_para_nivel2`.
2. **Uma exceção:** o agente volta uma task de `bloqueada` para `em-desenvolvimento` quando o desenvolvedor responde ao bloqueio que **ele mesmo** registrou (em modo prisma, a API permite essa única volta ao token do agente).
3. **Autorizar** (mover para `em-desenvolvimento` a partir de `backlog` ou `pronta`) é do desenvolvedor. O agente só faz isso quando o desenvolvedor pede **explicitamente** a mudança de status ("move a T-0007 para em-desenvolvimento"). **"Executa a T-0007" não é autorização.**
4. Toda mudança é registrada no histórico da task, com o autor (agente ou desenvolvedor) e o canal.
5. **Modo prisma:** o status **não existe no frontmatter**. O agente nunca move um status que o git já prova (PR aberto, PR mergeado): o webhook faz isso. Veja `rastreadores/prisma.md`.

### Status da HU

`aberta` (escrita) → `planejada` (tasks criadas) → `concluida` (depois de `fechar-hu`). `cancelada` se o desenvolvedor desistir.

## Onde cada coisa fica

```
AGENTS.md                      roteador (CLAUDE.md aponta para ele)
metodo/nucleo/                 o método. Vem do repositório central; não editar aqui
  VERSAO                       versão instalada
  fluxo.md                     este arquivo
  papeis/                      arquiteto, planejador, implementador, testador, revisores
  skills/<nome>/SKILL.md       como trabalhar
  modelos/                     hu, prd, task, decisao, skill-projeto, insumos-indice
  rastreadores/                local.md · prisma.md
  referencias/                 agent-skills.md (padrão de skills) · testes.md (o que testar) · seguranca.md (segredos, dados pessoais, conteúdo externo)
  scripts/                     tasks.mjs · gerar-adaptadores.mjs · segredos.mjs · mascarar.mjs
metodo/projeto/                deste projeto; preenchido no onboarding
  config.yaml                  autonomia, status, git, rastreador, comandos
  contexto.md                  o que é, para quem, ambientes, glossário, pessoas
  principios.md                regras inegociáveis deste projeto (≤ 15)
  skills/<nome>/SKILL.md       stack, padrões, design system, dados, deploy, cliente…
docs/trabalho/                 HUs, PRDs e tasks (fonte da verdade do conteúdo)
  INDICE.md                    gerado pelo script; não editar
  sugestoes-metodo.md          lições sobre o método (não sobre o projeto), para o repositório central
  HU-0001-slug/
    hu.md
    prd.md                     (só na trilha épico)
    tasks/T-0001-slug.md
  avulsas/tasks/T-0009-slug.md tasks sem HU (trilhas micro, bug, incidente)
docs/decisoes/ADR-0001-slug.md decisões de arquitetura (e riscos aceitos)
docs/insumos/                  o que o cliente mandou
  ESPECIFICACAO*.md            a especificação (versionada)
  INDICE.md                    um arquivo por linha: o que é, uso, autorização (versionado; as tasks leem este)
  brutos/                      originais: conversas, áudios, fotos, vídeos (FORA do git: LGPD e tamanho)
DESIGN.md                      direção visual aprovada (projetos com interface; criado na HU-0001); diz onde fica o tokens.css
docs/design/                   brief, rotas, capturas aprovadas (aprovado/)
qa/                            capturas e relatórios das skills de verificação (fora do git)
.agents/skills/ · .claude/     adaptadores gerados; não editar
```

IDs são sequenciais no projeto inteiro (HU-0001, T-0001, ADR-0001), então "tarefa 7" é sempre a T-0007. Para o próximo ID, rode `node metodo/nucleo/scripts/tasks.mjs proximo-id hu` (ou `task`, `adr`). Sem Node, procure o maior número existente e some 1.

## Como criar uma task avulsa (trilhas micro, bug, incidente e atalho)

1. ID: `node metodo/nucleo/scripts/tasks.mjs proximo-id task`.
2. Arquivo: `docs/trabalho/avulsas/tasks/T-XXXX-slug.md` a partir de `metodo/nucleo/modelos/task.md`, com `hu: avulsa`, `trilha:` preenchida e `cobre: []`. Os critérios de aceite ficam na própria task.
3. Preencha o que o DoR pede (objetivo, contexto, arquivos prováveis, passos, critérios, testes, fora do escopo). Na trilha bug, também a seção Diagnóstico.
4. Status inicial: `backlog` (micro e bug, aguardando autorização) ou `concluida` (atalho e incidente, quando o trabalho já foi feito com o desenvolvedor presente). **Nascer `concluida` não é uma transição**: é o registro de algo já feito e autorizado na conversa.
5. Rode `validar` e `indice`.

## Atalho

Só se o desenvolvedor disser explicitamente **"sem fluxo"**: o agente faz a mudança direto e, no fim, registra uma task já `concluida` em `docs/trabalho/avulsas/tasks/`, com `hu: avulsa` e o que foi feito. Nunca use o atalho por conta própria. A trilha micro já é curta; "sem fluxo" é para quando nem a rodada de entrevista cabe.

## Situações sem saída óbvia

| Situação | O que fazer |
|---|---|
| `config.yaml` ainda com `"NOME DO PROJETO"` | avise que o onboarding não foi feito e ofereça a skill `aplicar-metodo` antes de seguir |
| `git.usa_git: true`, mas a pasta não é repositório | pare e pergunte: `git init` ou mudar `usa_git` para `false`? |
| sem remoto ou sem `gh`/`glab` | trabalhe como nível 0 (commit local) e avise |
| skill listada na task não existe | trate como bloqueio: pergunte se cria a skill antes ou se segue sem ela |
| `rastreador: prisma` sem o MCP disponível | pare e avise; não troque para o modo local sozinho |
| task pedida não existe | diga, mostre as mais parecidas ou as autorizadas, e pergunte se é uma delas ou um pedido novo |
| a task usa um insumo com autorização `não confirmada` | implemente com marcador (`[AGUARDANDO AUTORIZAÇÃO]`) ou alternativa; não publique; registre a pendência no `INDICE.md` |
| pedido de interface num projeto sem `DESIGN.md` | proponha a HU de Direção visual antes; não escolha cores e fontes no meio de outra task |
| o desenvolvedor pede autonomia acima do padrão em risco alto, ou usar material sem autorização | avise uma vez, em uma frase; se ele confirmar, registre um ADR "Risco aceito" e siga |
| Node não instalado | faça à mão o que os scripts fariam (IDs, índice) e avise |
