---
name: registrar-decisao
description: Registra uma decisão de arquitetura (ADR) em docs/decisoes com contexto, alternativas e consequências. Use ao escolher biblioteca, serviço externo, padrão estrutural ou ao mudar algo de principios.md, no onboarding, no PRD ou durante uma task.
metadata:
  origem: metodo
  versao: "0.2.0"
---

# Registrar uma decisão (ADR)

ADR = Architecture Decision Record. Um arquivo curto que responde, daqui a um ano, "por que isso é assim?".

## Quando é obrigatório

- Dependência nova (biblioteca, serviço, API paga)
- Mudança de estrutura (pasta, camada, padrão de código novo)
- Mudança em `metodo/projeto/principios.md`
- Escolha entre abordagens no PRD (seção "Decisões") com impacto além da HU
- Qualquer coisa que o desenvolvedor disser "registra isso"

Detalhe que só vale para uma task fica no relatório da task, não vira ADR.

## Passos

1. ID: `node metodo/nucleo/scripts/tasks.mjs proximo-id adr`.
2. Copie `metodo/nucleo/modelos/decisao.md` para `docs/decisoes/ADR-XXXX-slug.md`.
3. Preencha: contexto (o problema, não a solução), decisão em 1 a 2 frases, alternativas com prós e contras reais, consequências (o que fica mais fácil, mais difícil, o que muda nos princípios e skills).
4. `status: proposta`. Mostre ao desenvolvedor. Com o "ok", `status: aceita`.
5. Se a decisão muda um princípio ou uma skill do projeto, faça a mudança no mesmo commit e cite o ADR nela.
6. Se substitui um ADR anterior, marque o antigo como `substituida` e aponte para o novo.

## Regras

- Um ADR por decisão. Curto: cabe numa tela.
- Nunca apague um ADR; substitua.
- O PRD aponta para os ADRs; não repete o conteúdo.
