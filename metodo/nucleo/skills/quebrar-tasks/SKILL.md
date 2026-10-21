---
name: quebrar-tasks
description: Divide uma HU (e seu PRD, se houver) em tasks autossuficientes, uma por PR, com dependências e status backlog. Use depois da HU ou do PRD aprovado.
metadata:
  origem: metodo
  versao: "0.4.0"
---

# Quebrar em tasks

Atue como o papel **planejador** (`metodo/nucleo/papeis/planejador.md`).

## Regra de ouro

**Uma task é autossuficiente.** Imagine um agente novo, numa sessão nova, que só recebe "execute a T-0012". Lendo **apenas** o arquivo da task e as skills que ela lista, ele precisa conseguir executar sem perguntar nada que a entrevista já respondeu. Copie para a task as decisões relevantes da HU e do PRD; não escreva só "ver HU".

## Tamanho

- **1 task = 1 PR revisável.** O desenvolvedor revisa em poucos minutos e o agente executa numa sessão, sem perder o fio.
- Grande demais: mexe em muitas áreas, tem mais de ~8 critérios de aceite ou exige "e também…". Divida.
- Pequena demais: não entrega nada verificável sozinha. Junte com a vizinha.
- Prefira **fatias verticais** (ex.: "lembrete 24h ponta a ponta para um tipo de consulta") a camadas soltas ("todas as tabelas", depois "todas as rotas").

## Passos

1. Leia HU, PRD e skills do projeto relacionadas.
2. Proponha a lista de tasks ao desenvolvedor **antes** de criar os arquivos:

   | # | Título | Cobre (CA) | Depende de | Complexidade | Risco | Resumo |
   |---|---|---|---|---|---|---|

   Complexidade de 1 a 7 (arquivos tocados, integrações, migração, incerteza). Se a estimativa passar de 7, quebre antes de propor. Toda task cobre pelo menos um CA, ou diz no objetivo que é infraestrutura para outra.

3. Com o "ok", para cada task:
   - ID: `node metodo/nucleo/scripts/tasks.mjs proximo-id task`
   - Arquivo: `docs/trabalho/HU-XXXX-slug/tasks/T-XXXX-slug.md`, a partir de `metodo/nucleo/modelos/task.md`
   - `status: backlog` (modo local). Em modo prisma, omita o campo `status`; o Prisma cria em backlog.
4. Rode `node metodo/nucleo/scripts/tasks.mjs validar` e corrija o que aparecer.
5. Rode `node metodo/nucleo/scripts/tasks.mjs indice`.
6. Se `rastreador` for `prisma`, crie as tasks no Prisma (`criar_task`) e **não** escreva `status:` no frontmatter (veja `metodo/nucleo/rastreadores/prisma.md`).
7. Atualize o `status` da HU para `planejada` e liste as tasks na seção Tasks da HU, com os CA que cada uma cobre.
8. Rode a skill `analisar-consistencia` e mostre o relatório.
9. Com o relatório limpo, diga ao desenvolvedor quais tasks podem começar primeiro e que ele precisa movê-las para `em-desenvolvimento` para autorizar.

## Checklist de cada task (Definition of Ready)

- [ ] Objetivo em uma frase
- [ ] Contexto suficiente (decisões copiadas, não só referenciadas)
- [ ] Arquivos ou áreas prováveis
- [ ] Passos sugeridos (o implementador pode melhorar)
- [ ] Critérios de aceite verificáveis (Dado / Quando / Então)
- [ ] Testes: cada CA com tipo (`interacao`, `integracao`, `unit`, `manual` com motivo); todo CA com interface tem `interacao`; dados e ambiente de teste
- [ ] Fora do escopo
- [ ] `depende_de` preenchido (ou vazio)
- [ ] `cobre` com os CA da HU
- [ ] `complexidade` entre 1 e 7
- [ ] `risco` e `sensivel` avaliados
- [ ] `skills:` com as skills do projeto obrigatórias
