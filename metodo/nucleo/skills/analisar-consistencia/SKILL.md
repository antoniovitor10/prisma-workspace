---
name: analisar-consistencia
description: Relatório somente leitura que cruza HU, PRD, tasks e princípios do projeto antes de pedir autorização para executar. Use depois de criar as tasks de uma HU e sempre que a HU, o PRD ou uma task mudar.
metadata:
  origem: metodo
  versao: "0.4.0"
---

# Analisar consistência

Portão de saída do planejamento. **Nunca edita arquivos**: produz um relatório. Problemas voltam para a etapa dona deles (HU → `escrever-hu`, PRD → `escrever-prd`, tasks → `quebrar-tasks`).

## O que checar

| Checagem | Severidade se falhar |
|---|---|
| Todo `CA` da HU aparece em `cobre:` de pelo menos uma task | CRÍTICO |
| Toda task tem pelo menos um `CA` em `cobre:` (ou é de infraestrutura e diz isso no objetivo) | ALTO |
| Todo `CA` com interface tem um teste de `interacao` na tabela "Testes" da task que o cobre | CRÍTICO |
| Todo `CA` coberto tem pelo menos uma linha na tabela "Testes"; `manual` sempre com motivo | ALTO |
| Nenhuma task contraria um item de `metodo/projeto/principios.md` | CRÍTICO |
| Nenhuma task contraria uma decisão do PRD ou um ADR aceito | ALTO |
| Dependências (`depende_de`) existem e não formam ciclo | CRÍTICO |
| Nenhuma task com `complexidade` fora de 1 a 7 | ALTO |
| Tasks com `risco: alto` ou `sensivel: true` estão marcadas como tal (dados pessoais, auth, pagamento, deploy, migração) | ALTO |
| Toda task cumpre o checklist de Definition of Ready (`quebrar-tasks`) | MÉDIO |
| Toda skill em `skills:` existe em `metodo/projeto/skills/` | MÉDIO |
| Duas tasks não fazem a mesma coisa; nenhuma mudança da HU ficou sem task | MÉDIO |
| Termos usados nas tasks batem com o glossário de `contexto.md` | BAIXO |

Rode também `node metodo/nucleo/scripts/tasks.mjs validar` e inclua o resultado.

## Relatório

```
## Análise de consistência · HU-XXXX · <data>
CRÍTICO (bloqueia autorização)
- …
ALTO (corrigir antes de executar)
- …
MÉDIO / BAIXO (anotar)
- …
Cobertura: CA-1 → T-0007 · CA-2 → T-0007, T-0008 · CA-3 → (nenhuma!)
Testes: CA-1 interacao + unit · CA-2 interacao · CA-3 manual (sem motivo!)
Resultado: PRONTO PARA AUTORIZAR | VOLTAR PARA <etapa>
```

Mostre ao desenvolvedor. Com zero CRÍTICO e ALTO (ou com os ALTO aceitos explicitamente por ele), diga quais tasks podem ser autorizadas primeiro.
