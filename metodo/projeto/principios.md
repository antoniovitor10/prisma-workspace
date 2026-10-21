# Princípios do projeto

<!-- Regras inegociáveis deste projeto. No máximo 15. Cada uma curta e verificável.
     O revisor de código, o revisor de segurança e a skill analisar-consistencia checam contra esta lista.
     Preenchido no onboarding (2026-10-21); muda só com decisão explícita do Vitor (registre um ADR). -->

1. Agente nunca faz push na `main`, merge, tag, release, `gh workflow run` ou deploy: merge na `main` publica em produção (`prisma.nordevs.com.br`, DETRAN). Quem integra é o Maestro, só com o revisor aprovando e o CI verde; task de risco alto espera o ok do Vitor. (G-DEPLOY)
2. Nenhuma migration ou mudança de schema sem a aprovação do Vitor registrada na task ou em ADR (G-MIGRATION); nunca editar migration aplicada; toda migration tem `Down` funcional.
3. O token do agente nunca move uma task para `em-desenvolvimento`: nem para autorizar (`backlog` ou `pronta`) nem para retomar um bloqueio, mesmo um que ele próprio registrou (a exceção de `metodo/nucleo/fluxo.md` não vale aqui); quem autoriza e retoma é o Vitor. Nenhum agente aprova portão humano; `status:` nunca vai no frontmatter de task (rastreador prisma).
4. Configuração e segredo só por variável de ambiente; nenhum segredo, `.env*`, storage state de navegador ou dado real em arquivo versionado, log, relatório ou comentário: o repositório é público.
5. ADR vigente em `docs/decisoes/` (ADR-0001 a ADR-0102 vieram da `DECISIONS.md`) não se re-discute; mudar exige ADR novo aprovado pelo Vitor, dizendo o que substitui.
6. Pedido que contradiz HU ou PRD aprovado, ADR vigente ou spec aprovada de referência para como pergunta: task `bloqueada` com a dúvida registrada, sem código (G-SCOPE).
7. Typecheck do frontend é `npx tsc -b` em `src/Prisma.Workspace.Web`; `tsc --noEmit` não checa nada neste projeto e não vale como evidência.
8. Teste vermelho é diagnóstico: proibido afrouxar asserção, prazo, retry ou pular teste para ficar verde.
9. Mudança em frontend, endpoint ou schema só conclui com os E2E afetados passando em `chromium-desktop` e `chromium-mobile`, com a saída real no relatório.
10. Controller sem regra de negócio: um caso de uso = um Command ou Query + Handler (MediatR); todo acesso a banco é `async`.
11. Interface usa só os tokens de `src/Prisma.Workspace.Web/src/styles/theme.ts` (D68), em tema claro e escuro, sem emoji e sem ícone ou biblioteca visual nova sem ADR.
12. Toda leitura e escrita respeita a organização e o acesso ao projeto; atribuir não concede acesso (D90); a IA interna só sugere escritas, com aprovação humana (D91).
13. Regras de workflow, transições e o histórico imutável (`StageHistory`, histórico do método) não mudam sem aprovação explícita do Vitor (G-WORKFLOW, G-HISTORY).
14. Produção e serviços alheios são intocáveis: contêineres `prisma-workspace-api`, `prisma-ai-bridge`, `detran-kanban-*` (inclui o banco de produção) e `prisma-workspace-api-rollback-*`, `/home/dev`, `/opt`, Caddy, firewall, cron e SSH.
15. Código e identificadores em inglês; textos, comentários e documentos em pt-BR, sem emojis; nenhuma menção institucional ao DETRAN na superfície do produto (D68).
