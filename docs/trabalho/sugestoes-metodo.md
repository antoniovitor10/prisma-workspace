# Sugestões para o método

Lições sobre o método (não sobre o projeto), para levar ao repositório central.

## 2026-10-21 · Retomada de bloqueio pelo token do agente

- **Onde:** `metodo/nucleo/fluxo.md:125` e `metodo/nucleo/rastreadores/prisma.md:40` e `:58`.
- **O que acontece:** o núcleo deixa o agente mover `bloqueada` para `em-desenvolvimento` quando o bloqueio foi registrado por ele. No Prisma WorkSpace, a regra do Vitor é que o token do agente nunca move para `em-desenvolvimento`, em nenhum caso; o projeto registra isso no princípio 3 e no ADR-0103.
- **Sugestão:** tornar a exceção uma opção do `config.yaml` (por exemplo `agente_pode_retomar_bloqueio: true | false`), em vez de regra fixa do núcleo.
- **Origem:** revisão do Esquadro dos PRs #41 e #42.
