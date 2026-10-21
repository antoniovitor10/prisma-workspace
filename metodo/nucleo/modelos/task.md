---
id: T-XXXX
titulo: ""
hu: HU-XXXX           # ou "avulsa" para tasks sem HU (trilhas micro, bug, incidente)
trilha: funcionalidade  # micro | bug | funcionalidade | epico | incidente
status: backlog       # (em modo prisma este campo NÃO existe: o status vive no Prisma)
depende_de: []        # ex.: [T-0003, T-0004]
cobre: []             # CA da HU que esta task cumpre, ex.: [CA-1, CA-3]. Em task avulsa fica vazio: os CA vivem aqui mesmo
complexidade: 3       # 1 a 7 (arquivos, integrações, migração, incerteza); se passar de 7, quebre a task
risco: normal         # normal | alto
sensivel: false       # true se toca dados pessoais, auth, pagamento, segredos
skills: []            # skills do projeto obrigatórias, ex.: [stack, design-system]
criada_em: AAAA-MM-DD
# Métricas: o agente preenche ao entregar (executar-task, passo 9); o desenvolvedor preenche revisao_humana e homologacao.
ferramenta: ""        # quem executou, ex.: claude-code/opus, codex, copilot
voltas: null          # reprovações internas do testador e dos revisores (0 a 2)
bloqueios: null       # quantas vezes a task foi para bloqueada
ca_nao_verificados: null
testes_criados: null  # testes novos ou alterados
testes_pegam: null    # quantos passaram na prova de que o teste pega
revisao_humana: ""    # aprovado | ajustes | refeito (desenvolvedor, na revisão do PR)
homologacao: ""       # ok | falhou | nao-se-aplica (desenvolvedor ou quem homologa)
---

# T-XXXX · <título>

## Objetivo

<Uma frase: o que estará diferente quando esta task terminar.>

## Contexto

<Tudo que um agente sem a conversa precisa saber. Decisões da HU/PRD copiadas aqui, não só referenciadas.>

## Arquivos / áreas prováveis

- `caminho/arquivo` — o que muda

## Insumos

<Arquivos de `docs/insumos/` que esta task usa, com o uso e a autorização copiados do `INDICE.md`. Caminho relativo a `docs/insumos/brutos/`, como no índice. Ex.: `marca/logo.svg` (asset final, autorização sim). Se não usa nenhum, escreva "nenhum".>

## Passos sugeridos

1. …

## Critérios de aceite

- **CA-1:** Dado …, quando …, então ….

## Testes

<Preenchido pelo planejador: o que testar. O testador escreve e roda **depois** da implementação (`metodo/nucleo/referencias/testes.md`). Todo CA com interface tem um teste de `interacao`; `manual` só com motivo.>

| CA | Tipo | O que o teste simula e confere |
|---|---|---|
| CA-1 | interacao | <abre a tela X, toca em Y, vê Z> |
| CA-2 | unit | <função W com a entrada V devolve U> |

**Dados e ambiente:** <usuário de teste, massa de dados, emulador ou tamanho de tela, serviço que precisa estar rodando>

## Fora do escopo

- …

## Diagnóstico

<Só na trilha bug: reprodução, causa raiz e evidência (skill depurar-bug).>

## Bloqueios

<Preenchido durante a execução quando há dúvida.>

## Handoff

<Preenchido só se a sessão terminar com a task incompleta (skill passar-bastao): feito, falta, comandos, arquivos, dúvidas.>

## Execução

<Relatório preenchido pelo agente ao terminar (skill executar-task), com a saída real dos comandos (skill verificar-antes-de-concluir).>

## Roteiro de homologação

<Escrito pelo testador quando a task vai para em-revisao (modelo em metodo/nucleo/referencias/testes.md). Quem homologa marca o resultado aqui.>

## Histórico

- AAAA-MM-DD · criada (backlog) · planejador
