# US-METODO-GIT-001: Status das tasks do método provado pelo Git

**Status:** active
**Módulo:** metodo-git
**Spec vinculada:** `SPEC-METODO-GIT-INTEGRATION`

## História

**Como** desenvolvedor que trabalha com agentes pelo método
**Quero** que o Prisma acompanhe o status das tasks a partir do que acontece no GitHub e recuse que um agente se autorize
**Para** ver no quadro, sem editar arquivos nem padronizar títulos, o que está autorizado, em revisão, em teste e concluído

## Cenários esperados

- **Dado** uma task em desenvolvimento vinculada a uma branch ou a um PR, **quando** o PR é aberto no GitHub, **então** o Prisma move a task para em revisão e registra o fato no histórico com o canal webhook.
- No repositório da equipe do DETRAN, onde o método fica fora do Git e o título do PR é livre, a task é achada pelo vínculo explícito ou pela branch.
- Um token de agente nunca move uma task de backlog ou pronta para em desenvolvimento, por nenhum canal.
- A mesma entrega do webhook recebida duas vezes não gera duas transições, e uma assinatura inválida é recusada sem efeito.

## Observação humana

Em 2026-10-16, o PO pediu o Prisma como rastreador do método e a integração com o Git (D100 e D101): GitHub, inclusive
no repositório da equipe do DETRAN, com GitHub App, começando por papéis na API e webhook.
