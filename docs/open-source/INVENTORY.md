# Inventário do snapshot open source

**Data:** 2026-09-05
**Origem privada:** `runrun`
**Destino:** `antoniovitor10/prisma-workspace`
**Estado:** primeira extração privada, ainda sem commit

## Política

O snapshot é formado por allowlist. Ausência de um caminho na lista não autoriza sua publicação automática.

## Copiado para revisão e sanitização

- `src/`: produto, excluindo `.env*`, builds, dados de runtime e estado autenticado do Playwright.
- `tests/`: testes .NET.
- `scripts/`: scripts gerais; cada script ainda precisa de revisão de portabilidade e referências privadas.
- `specs/`, `stories/`, `context/`, `workflows/`, `profiles/`, `agents/`: governança SDD/AI-Native.
- `tools/`: ferramentas de desenvolvimento, excluindo dependências, builds e snapshots gerados.
- Arquivos raiz canônicos: `AGENTS.md`, `DECISIONS.md`, `ROADMAP.md`, `PROGRESS.md`, `backlog.md`, solution e Dockerfile.

## Não copiado

- `.git/`: histórico do repositório institucional.
- `.agent-state/`, `.runlogs/`, `.artifacts/`: estado e saídas locais.
- `docs/entrada/`: documentos, imagens e extrações institucionais.
- `migracao/`: integração/importação específica do sistema anterior.
- `.github/workflows/deploy.yml`: deploy acoplado à infraestrutura privada.
- `.env*`: configurações locais, de E2E e produção.
- `DEPLOY.md`, `RODAR-LOCAL.md`, `HANDOFF.md`: instruções e handoffs ligados ao ambiente anterior.
- `docs/context-explorer.html` e `tools/context-explorer/**/model.json`: artefatos regeneráveis.
- `out1.txt`, caches, builds, resultados de teste, logs, backups e dados de runtime.

## Achados de segurança

- A primeira varredura detectou um estado autenticado do Playwright em
  `src/Detran.Kanban.Web/e2e/.auth/user.json`.
- A cópia foi removida do snapshot antes de qualquer commit ou push.
- O arquivo continua apenas na origem privada e deve ser considerado credencial temporária revogável.

## Itens em revisão obrigatória

- Referências `Detran`, `Runrun`, domínios e endereços de infraestrutura.
- Dockerfile, scripts e nomes `Detran.Kanban.*`.
- Fixtures, seeds e exemplos que possam conter identidades reais.
- Assets cuja autoria ou licença não esteja comprovada.
- Licenças das dependências e compatibilidade da licença do projeto.
- Migrations EF Core: preservar; não remover ou compactar sem `G-MIGRATION`.

## Gate do primeiro commit

- [x] Gitleaks com zero achados no snapshot.
- [x] Teste automático da denylist.
- [x] Lista de referências legadas gerada sem valores secretos.
- [x] Arquivos fundamentais de build presentes e baseline compilado/testado.
- [ ] Diff do snapshot revisado.
