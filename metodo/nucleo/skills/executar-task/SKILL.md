---
name: executar-task
description: Executa uma task autorizada do início ao PR/MR, passando pelos papéis implementador, testador e revisores, e registra o relatório na task. Use quando o desenvolvedor pedir para executar, fazer ou pegar uma task, por número ou descrição.
metadata:
  origem: metodo
  versao: "0.5.0"
---

# Executar uma task

## 1. Identificar

- **Por número** ("faz a 7", "T-0007"): procure `docs/trabalho/*/tasks/T-0007-*.md`.
- **Por descrição** ("faz a do botão do WhatsApp"): rode `node metodo/nucleo/scripts/tasks.mjs listar` e procure pelo título. Se houver mais de uma candidata, mostre as opções.
- **Não encontrou:** diga isso, mostre as tasks mais próximas (ou as autorizadas) e pergunte se é uma delas ou um pedido novo (que vai para a skill `entrevistar`).

## 2. Checar (antes de confirmar)

Sem alterar nada, verifique:

- [ ] o status está em `status.agente_pode_iniciar` (`config.yaml`)
- [ ] toda task em `depende_de` está `concluida`. Se não estiver, pergunte se pode criar a branch a partir da branch da dependência
- [ ] todas as skills em `skills:` existem
- [ ] a task cumpre o checklist de Definition of Ready (skill `quebrar-tasks`; em task avulsa, `cobre` fica vazio e os CA estão na própria task)
- [ ] `node metodo/nucleo/scripts/tasks.mjs validar` sem erros
- [ ] git: a pasta é repositório e a árvore está limpa (se `git.usa_git: true`)
- [ ] rastreador disponível (se não for `local`)
- [ ] `config.yaml` não está no modelo (`"NOME DO PROJETO"`)
- [ ] a task tem seção **Handoff** preenchida? Se sim, leia-a e confira o git antes de continuar (skill `passar-bastao`)

## 3. Confirmar

Mostre a task **e** o resultado da checagem numa única mensagem:

> É a **T-0007 · Botão de WhatsApp no cabeçalho** (HU-0004), status `em-desenvolvimento`, risco normal. Checagem ok. Executo?

Ou, com problema:

> É a **T-0007 · Botão de WhatsApp no cabeçalho** (HU-0004)? Não posso executar ainda: está em `backlog` (precisa de `em-desenvolvimento`) e a skill `design-system` não existe. Quer mover a task e criar a skill antes?

Sem "sim" (e sem os problemas resolvidos), não continue. Problemas no git ou no config: veja "Situações sem saída óbvia" em `metodo/nucleo/fluxo.md`.

## 4. Preparar

1. Leia a task inteira, a HU e o PRD vinculados, e **todas** as skills listadas em `skills:`.
2. Leia `config.yaml`: autonomia (use `autonomia_risco_alto` se `risco: alto`), comandos, git e revisão de segurança.
3. Se `git.usa_git: true`: atualize a branch principal (`git pull`, se houver remoto) e crie a branch da task (`git.padrao_branch`). Nunca descarte mudanças não commitadas do desenvolvedor. Com `metodo_fora_do_git: true`, branch, commits e PR seguem os padrões da equipe e **não citam** o ID da task nem arquivos do método; o vínculo com a task fica só no relatório dela.
4. Registre o início no histórico da task.

## 5. Rodar os papéis

Se a ferramenta suporta subagentes (Claude Code, Maestri…), rode cada papel num subagente próprio, passando o arquivo do papel e a task. Se não suporta, faça em sequência na mesma sessão: **leia o arquivo do papel antes de cada etapa e escreva a saída daquele papel, no formato de entrega dele, antes de passar ao próximo.**

```
implementador ─► testador ─► revisor-codigo ─► revisor-seguranca (se aplicável)
      ▲              │              │                  │
      └──── ajustes ─┴──────────────┴──────────────────┘
```

Arquivos: `metodo/nucleo/papeis/<papel>.md`. Quando a revisão de segurança entra está em `revisor-seguranca.md`.

O testador entra **depois** do implementador: escreve os testes da tabela "Testes" da task (com teste de interação para todo CA com interface), roda, prova que pegam e escreve o roteiro de homologação. Se precisar de identificador no elemento (`testID`, `data-testid`) e ele não existir, devolve ao implementador.

**Limite: 2 voltas por task**, somando todos os papéis. Na terceira reprovação: status `bloqueada`, registre os pontos e pergunte ao desenvolvedor.

## 6. Bloqueio por ambiguidade

A qualquer momento da execução, se surgir algo que a task, a HU, o PRD e as skills não respondem **e** a resposta muda o resultado:

1. Pare de implementar (commit do que já está feito, se fizer sentido, com `wip(T-XXXX): …`).
2. Mude o status para `bloqueada`.
3. Na seção `## Bloqueios` da task, escreva a pergunta com opções e uma recomendação.
4. Pergunte ao desenvolvedor na conversa.
5. Com a resposta: registre a decisão na task (para o próximo agente), volte para `em-desenvolvimento` e continue.

Detalhe sem impacto real (nome de variável interna, ordem de imports) não é ambiguidade: siga o padrão do projeto.

### Contradição com regra escrita

Regra escrita é o que está em `principios.md`, nas skills do projeto ou num ADR aceito. Se, durante a execução ou na verificação, o que a task pede (ou o que você fez) **contradiz** uma regra escrita:

1. Pare antes de concluir. Não contorne a regra em silêncio e não a edite por conta própria.
2. Mostre ao desenvolvedor, numa mensagem só:
   > Está escrito em `<arquivo>`: "<regra atual>". O que esta task faz muda isso: <o quê>. Proposta de texto novo: "<regra nova>". Opções: (a) atualizar a regra para o texto novo · (b) manter a regra e ajustar a task · (c) é exceção só desta task (registro o porquê na task).
3. Com (a): atualize o arquivo (skill `criar-skill-projeto`; princípio ou ADR: com `registrar-decisao`, um ADR novo que substitui o antigo), com o ID da task como origem. Com (b): volte ao implementador. Com (c): registre a exceção no relatório.
4. Para não ler o projeto inteiro: confira só `principios.md` (curto) e as skills do projeto cujo assunto ou caminhos o diff encosta. Regra que você não abriu não pode ser "contradita sem saber"; por isso a leitura do passo 4 (Preparar) inclui as skills da task.

Se a sessão precisar terminar com a task incompleta (limite de contexto, pedido do desenvolvedor, fim do expediente), siga `passar-bastao` antes de parar.

## 7. Verificar antes de concluir

Siga a skill `verificar-antes-de-concluir`: saída real de lint, testes e build no relatório; cada CA da task conferido um a um; escopo e sujeira checados. Nada passa daqui sem evidência.

## 8. Entregar conforme a autonomia

| Nível | Ação final | Status final |
|---|---|---|
| 0 supervisionado | commits na branch, **sem push** | `em-revisao` |
| 1 aprovar-merge | push e PR/MR aberto (`gh pr create` / `glab mr create`) com o relatório no corpo | `em-revisao` |
| 2 autônomo | com tudo verde: merge na principal e `comandos.deploy`, se existir | `em-teste` se `tem_homologacao: true`, senão `concluida` |

Use `autonomia_risco_alto` no lugar de `autonomia` quando a task tem `risco: alto`. Sem plataforma disponível (sem `gh`/`glab` ou sem remoto), caia para o nível 0 e avise. Se a task foi para `concluida`, veja se a HU também conclui (`fluxo.md`, "Status da HU").

## 9. Relatório na task

Preencha a seção `## Execução` da task:

```
### Execução · <data>
**Branch:** task/T-0007-botao-whatsapp · **PR:** <link ou "não aberto (nível 0)">
**O que mudou:** <arquivos e resumo>
**Decisões tomadas:** <e por quê>
**Verificação (testador):**
| CA | Tipo | Teste / como verifiquei | Pega? | Resultado |
**Testes criados ou alterados:** <arquivos>
**Linha de base:** <testes, e2e, lint e build antes × depois>
**Revisões:** código ✅ · segurança ✅ / não aplicável
**Roteiro de homologação:** <na seção própria da task, ou "não se aplica">
**Pendências / sugestões fora do escopo:** <ou "nenhuma">
**Proposta para skills do projeto:** <regra nova descoberta, ou "nenhuma">
```

Preencha também as **métricas** do frontmatter: `ferramenta`, `voltas`, `bloqueios`, `ca_nao_verificados`, `testes_criados`, `testes_pegam`. Números reais da execução, não estimativa. `revisao_humana` e `homologacao` ficam vazios: são do desenvolvedor.

Depois: atualize o status e o histórico, rode `tasks.mjs indice`. **Modo prisma:** comente o relatório e vincule o PR (`vincular_pr`); não mova o status que o git vai provar (PR aberto, merge): o webhook faz isso. No nível 0, sem push, mova para `em-revisao` pelo MCP.

Se esta era a última task da HU e ela foi para `concluida`, ofereça: "Fecho a HU-XXXX (convergir + retrospectiva)?" (skill `fechar-hu`).

## 10. Fechar a conversa

Mostre ao desenvolvedor, curto: o que foi feito, o resultado da verificação, o link do PR (ou a branch), as pendências e a proposta de regra para as skills (se houver), perguntando se aplica.

## Regras

- Uma task por vez. Não emende a próxima sem pedido.
- Regra escrita nunca muda sem o "sim" do desenvolvedor, e nunca é contornada em silêncio (seção "Contradição com regra escrita").
- Nunca faça merge ou deploy acima do nível de autonomia, mesmo que peçam "rapidinho". Para isso, o desenvolvedor muda a autonomia ou faz ele mesmo.
- Nunca altere os critérios de aceite para "passar". Se um critério está errado, bloqueie e pergunte.
