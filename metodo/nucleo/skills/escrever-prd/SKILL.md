---
name: escrever-prd
description: Escreve o PRD (documento de requisitos com abordagem técnica) de uma HU grande, atuando como arquiteto. Use quando a HU foi aprovada com PRD; antes de quebrar em tasks.
metadata:
  origem: metodo
  versao: "0.2.0"
---

# Escrever o PRD

Atue como o papel **arquiteto** (`metodo/nucleo/papeis/arquiteto.md`).

## O que é o PRD aqui

**Product Requirements Document**: a descrição completa do que será construído. Neste método ele tem duas metades:

1. **Requisitos (o quê e por quê):** problema, objetivo, usuários, requisitos funcionais e não funcionais, fora do escopo
2. **Abordagem técnica (como):** onde muda, dados, integrações, decisões, riscos, ordem de construção

## Passos

1. Leia a HU, o contexto do projeto, as skills do projeto relacionadas e o código afetado.
2. Copie `metodo/nucleo/modelos/prd.md` para `docs/trabalho/HU-XXXX-slug/prd.md`.
3. Preencha os requisitos a partir da HU. **Não acrescente requisito** que a HU não sustenta.
4. Desenhe a abordagem técnica:
   - Siga os padrões existentes do projeto. Divergir é uma **decisão** e precisa de justificativa.
   - Para cada decisão não óbvia, registre: contexto, opções consideradas, escolha e motivo. Decisão com impacto além desta HU (dependência nova, estrutura, princípio) vira ADR (skill `registrar-decisao`); o PRD só aponta para ele.
   - Liste o que precisa existir antes (contas, chaves, acessos, migrações).
5. Toda incerteza vai para **Dúvidas em aberto**. Faça rodadas de perguntas com o desenvolvedor (como na entrevista) até zerar, ou até ele aceitar explicitamente uma premissa.
6. Mostre um resumo: decisões principais, riscos e uma proposta de divisão em blocos.
7. Pergunte: **"PRD ok? Posso quebrar em tasks?"**

## Regras

- Dúvidas em aberto precisam estar **vazias** (ou aceitas como premissa) antes de quebrar em tasks.
- Se o PRD revelar que a HU estava errada ou incompleta, atualize a HU e avise o desenvolvedor.
- Diagramas simples em Mermaid são bem-vindos quando há fluxo entre sistemas.
