---
name: escrever-hu
description: Escreve a história de usuário (HU) em docs/trabalho a partir de um escopo fechado na entrevista, com critérios de aceite e recomendação de PRD ou não. Use logo após o desenvolvedor dizer que o escopo fecha.
metadata:
  origem: metodo
  versao: "0.5.0"
---

# Escrever a HU

Pré-requisito: um escopo fechado (skill `entrevistar`) com "sim" do desenvolvedor. Se não existe, volte para a entrevista.

## Passos

1. **ID:** rode `node metodo/nucleo/scripts/tasks.mjs proximo-id hu`. Exemplo: `HU-0004`.
2. **Pasta:** `docs/trabalho/HU-0004-<slug>/`, com slug curto, minúsculo e com hífens (`botao-whatsapp-cabecalho`).
3. **Arquivo:** copie `metodo/nucleo/modelos/hu.md` para `hu.md` na pasta e preencha.
4. **História:** "Como **<quem>**, quero **<o quê>**, para **<benefício>**." Escrita do ponto de vista de quem usa, não de quem programa.
5. **Critérios de aceite:** um por comportamento verificável, no formato Dado / Quando / Então, numerados `CA-1`, `CA-2`… Cada critério precisa ser testável por alguém que não estava na conversa. As tasks vão apontar para esses números em `cobre:`.
6. **Decisões da entrevista:** registre as respostas importantes (cor, texto, link…). É o que impede um agente futuro de perguntar de novo ou decidir diferente. Preencha `rodadas_entrevista` e `perguntas_entrevista` no frontmatter (perguntas de decisão; confirmações não contam).
7. **Tamanho e PRD.** Recomende PRD se **qualquer** um destes valer:
   - precisa de mais de 3 tasks
   - cria ou altera modelo de dados, ou exige migração
   - envolve integração externa nova
   - envolve mais de uma área do sistema (ex.: back-end + painel + app)
   - tem risco alto (dados pessoais, pagamento, autenticação, produção)
   - existe mais de uma abordagem técnica razoável
8. Mostre a HU ao desenvolvedor e pergunte: **"HU ok? Recomendo [com/sem] PRD porque <motivo>."**

## Depois

1. Sempre: skill `clarificar` (até 5 perguntas; respostas entram na seção Esclarecimentos).
2. **Com PRD** (trilha épico) → skill `escrever-prd`
3. **Sem PRD** → skill `quebrar-tasks` (mesmo que resulte em 1 task)

## Regras

- A HU descreve **o quê** e **por quê**. O **como** vai no PRD ou nas tasks.
- Nada na HU pode contradizer o escopo aprovado. Se surgir dúvida nova, pergunte.
- O frontmatter precisa ter `id`, `titulo`, `trilha`, `status: aberta` e `prd: sim|nao`.
