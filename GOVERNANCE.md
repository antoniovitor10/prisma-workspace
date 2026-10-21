# Governança

O Prisma WorkSpace adota desenvolvimento aberto com decisões rastreáveis.

## Papéis

- **Product Owner:** define visão, prioridade e escopo, aprova HUs e autoriza tasks.
- **Maintainers:** revisam código, segurança, releases e documentação.
- **Contributors:** propõem issues, HUs, código, testes e documentação.

## Decisões

- O trabalho segue o método em `AGENTS.md` e `metodo/`: HU, tasks, revisão e integração.
- `metodo/projeto/contexto.md` resume o produto e o roadmap; `metodo/projeto/principios.md` traz as regras inegociáveis.
- `docs/decisoes/` registra as decisões de arquitetura (ADR); uma decisão só muda com um ADR novo.
- Os contratos vigentes de cada área estão nas referências da skill `metodo/projeto/skills/dominio`.
- Mudanças sensíveis (schema, permissões, histórico, workflow, deploy) esperam a aprovação do Product Owner.

Nenhuma automação ou agente de IA autoriza tasks nem aprova mudanças sensíveis. Mudanças incompatíveis devem ser discutidas antes da
implementação e registradas de forma auditável.

## Releases

O projeto adotará SemVer. Política de branches, cadência, mantenedores iniciais e quórum para decisões serão fechados
antes do primeiro release público; até lá, `main` permanece uma linha privada de preparação.
