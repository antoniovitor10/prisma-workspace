# Roteiro provisório de revisão das propostas da Oficina

20/10/2026. Base comum a2797a7. Todos os PRs permanecem em rascunho; REVISAO e QA oficiais pendentes. A fila de validação continua ativa. Este documento organiza a leitura das propostas e os pontos de contato; os resultados atuais vivem nos relatórios de cada branch e no ESTADO externo.

## Ordem sugerida de leitura

1. PR29:auditoria do backlog e limites de gate/schema/risco, antes de atribuir conclusão às tasks.
2. PR27:contrato principal de QA, testesL06/L07/L08 e D99; observar retificações de contagem, esperadoB07/overlap e full pendente.
3. PR28:organizações, concorrência/inatividade e foco.
4. PR30:diagnóstico e correções comprovadas de estabilidade, com hipóteses sem prova separadas. O ajuste semanal e a espera unitária do editor também foram transportados a outras propostas.
5. PR32 e PR34:dependências npm/NuGet e suas contraprovas; PR36:SBOM/proveniência depende das duas correções e as incorpora como propostas, com proveniência unsigned.
6. PR31,PR33,PR37,PR38,PR40:chave oculta, ordem do cartão, metodologia, colunas e estado de navegação. Reler os comportamentos combinados nos arquivos compartilhados abaixo.
7. PR35 e PR39:operação Community, recuperação ensaiada e neutralização documental; licença/snapshot público continuam próprios.

## Revisões publicadas nesta conferência

Snapshot 2026-10-20T18:28:41.526497+00:00. Todos os14 PRs confirmados em rascunho. SHAs observados antes deste commit documental.

| PR | SHA |
|---|---|
| [PR27](https://github.com/antoniovitor10/prisma-workspace/pull/27) | a0a4522 |
| [PR28](https://github.com/antoniovitor10/prisma-workspace/pull/28) | 9bad280 |
| [PR29](https://github.com/antoniovitor10/prisma-workspace/pull/29) | b07701d |
| [PR30](https://github.com/antoniovitor10/prisma-workspace/pull/30) | 7b39027 |
| [PR31](https://github.com/antoniovitor10/prisma-workspace/pull/31) | 37c566e |
| [PR32](https://github.com/antoniovitor10/prisma-workspace/pull/32) | 6e261e1 |
| [PR33](https://github.com/antoniovitor10/prisma-workspace/pull/33) | 13b76c5 |
| [PR34](https://github.com/antoniovitor10/prisma-workspace/pull/34) | caa3267 |
| [PR35](https://github.com/antoniovitor10/prisma-workspace/pull/35) | c9afba5 |
| [PR36](https://github.com/antoniovitor10/prisma-workspace/pull/36) | 3bc3363 |
| [PR37](https://github.com/antoniovitor10/prisma-workspace/pull/37) | f567171 |
| [PR38](https://github.com/antoniovitor10/prisma-workspace/pull/38) | 6cb5e90 |
| [PR39](https://github.com/antoniovitor10/prisma-workspace/pull/39) | 52ee3be |
| [PR40](https://github.com/antoniovitor10/prisma-workspace/pull/40) | 7188864 |

PR27:código e9023e7 isola sincronização; novo focal/full local pendente após230PASS/3FAIL/40skips (dois cenários de seed compartilhado e um timeoutC6 sem causa). PR30:código7b39027 inclui espera unitária pelo estado carregado do portal;168PASS/43arquivos, nativo completo local pendente. PR40:código7188864 usa query atual no clique;182PASS/45arquivos eCI123PASS/40skips; novo focal/full local pendente. PR38:full local123PASS/40skips/0FAIL e focal5PASS. Resultados remotos e locais permanecem separados.

## Arquivos de produto/testes compartilhados

29 caminhos comuns, derivados somente dos nomes do diff contra a2797a7. Caminho comum não implica conflito textual; patches idênticos transportados são a mesma correção.

| Arquivo | PRs |
|---|---|
| `src/Prisma.Workspace.Web/src/components/RichTaskDescriptionEditor.test.tsx` | 27, 30, 34, 36, 37, 38 |
| `src/Prisma.Workspace.Web/e2e/fixtures/boards.ts` | 27, 30, 38, 40 |
| `src/Prisma.Workspace.Web/src/pages/Kanban.tsx` | 27, 33, 37, 38 |
| `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.test.tsx` | 27, 31, 37 |
| `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx` | 27, 31, 37 |
| `src/Prisma.Workspace.Web/src/features/reports/ReportsHub.tsx` | 27, 31, 37 |
| `src/Prisma.Workspace.Web/src/pages/Kanban.styles.ts` | 27, 30, 38 |
| `src/Prisma.Workspace.Infrastructure/Prisma.Workspace.Infrastructure.csproj` | 34, 36 |
| `src/Prisma.Workspace.Infrastructure/Repositories/OrganizationRepository.cs` | 27, 28 |
| `src/Prisma.Workspace.Web/e2e/board-stage-sync.spec.ts` | 27, 30 |
| `src/Prisma.Workspace.Web/e2e/document-alignment.spec.ts` | 27, 30 |
| `src/Prisma.Workspace.Web/e2e/fixtures/seedProject.ts` | 27, 30 |
| `src/Prisma.Workspace.Web/e2e/fixtures/test.ts` | 27, 30 |
| `src/Prisma.Workspace.Web/e2e/task-detail-flow.spec.ts` | 27, 37 |
| `src/Prisma.Workspace.Web/e2e/weekly-time-reachability.spec.ts` | 27, 30 |
| `src/Prisma.Workspace.Web/package-lock.json` | 32, 36 |
| `src/Prisma.Workspace.Web/package.json` | 32, 36 |
| `src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx` | 37, 40 |
| `src/Prisma.Workspace.Web/src/layout/Topbar.test.tsx` | 27, 28 |
| `src/Prisma.Workspace.Web/src/layout/Topbar.tsx` | 27, 28 |
| `src/Prisma.Workspace.Web/src/pages/ProjectKanbanDirect.test.tsx` | 37, 38 |
| `src/Prisma.Workspace.Web/src/pages/ProjectWiki.tsx` | 27, 31 |
| `src/Prisma.Workspace.Web/src/pages/ProjectWorkspace.tsx` | 31, 40 |
| `src/Prisma.Workspace.Web/src/pages/Requests.tsx` | 27, 31 |
| `src/Prisma.Workspace.Web/src/pages/Teams.tsx` | 27, 31 |
| `src/Prisma.Workspace.Web/src/security/editorAttributes.test.ts` | 32, 36 |
| `src/Prisma.Workspace.Web/src/test/seedProject.test.ts` | 27, 30 |
| `tests/Prisma.Workspace.Tests/Infrastructure/HtmlSanitizerServiceTests.cs` | 34, 36 |
| `tests/Prisma.Workspace.Tests/ProjectFeatureTests.cs` | 28, 31 |

## Critérios de revisão combinada

- Preservar nome completo obrigatório, erro400 por campo e proteção de segredo do PR27 ao revisar os demais caminhos de criação/edição.
- A chave continua em contratos técnicos; nomes/referências humanas seguem o recorte do PR31. URL não transporta credencial ou conteúdo de tarefa.
- Coluna visual muda Position pelo endpoint existente; classificação/transições/WIP e história mantêm suas regras. A ordem manual de cartões é outro comportamento.
- Pontos aparecem independentemente do valor interno de metodologia, com DTO/enum legados preservados. Filtros/árvore/item/seleção/rolagem usam a URL no backlog.
- Guardas de resposta antiga, rollback e cancelamento continuam necessários quando os consumidores de Kanban/Drawer/Backlog forem lidos juntos.
- Manter teste terminal do editor e privacidade do reporter, distinguir diagnóstico confirmado de hipótese, e preservar erros/execuções anteriores nos relatórios.
- SBOM deve incluir devDependencies na parte frontend, runtime separado e quatro subjects/hashes conferidos; não atribuir assinatura ou nívelSLSA à proveniência unsigned.
- Documentação de recuperação preserva banco/anexos/chaves/configuração; o ensaio de instalação/restauração não equivale a upgrade entre versões nem publicação pública.

As tasks com migration/gate/risco alto, homologações oficiais e dados reais proibidos continuam pendentes conforme auditoria. A varredura final somente leitura será produzida depois de esgotar a fila elegível e suas validações; não há relatório final de varredura neste checkpoint.
