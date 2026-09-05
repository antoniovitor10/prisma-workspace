# Governança

O Prisma WorkSpace adota desenvolvimento aberto com decisões rastreáveis.

## Papéis

- **Product Owner:** define visão, prioridade, escopo e aprova Human Gates.
- **Maintainers:** revisam código, segurança, releases e documentação.
- **Contributors:** propõem issues, specs, código, testes e documentação.

## Decisões

- `ROADMAP.md` define fases e prioridade.
- `DECISIONS.md` registra decisões técnicas consolidadas.
- `stories/` preserva intenção funcional humana.
- specs aprovadas em `specs/` são o contrato de implementação.
- mudanças sensíveis seguem os Human Gates descritos em `AGENTS.md`.

Nenhuma automação ou agente de IA aprova gates humanos. Mudanças incompatíveis devem ser discutidas antes da
implementação e registradas de forma auditável.

## Releases

O projeto adotará SemVer. Política de branches, cadência, mantenedores iniciais e quórum para decisões serão fechados
antes do primeiro release público; até lá, `main` permanece uma linha privada de preparação.
