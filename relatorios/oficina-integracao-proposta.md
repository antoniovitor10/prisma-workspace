# Roteiro de revisão das propostas da Oficina

20/10/2026. Base comum a2797a7. Todos os PRs permanecem em rascunho; REVISAO e QA oficiais pendentes. A fila elegível deste goal foi percorrida; resultados e ressalvas foram registrados. Este documento organiza a leitura das propostas e os pontos de contato; os resultados atuais vivem nos relatórios de cada branch e no ESTADO externo.

## Ordem sugerida de leitura

1. PR29:auditoria do backlog e limites de gate/schema/risco, antes de atribuir conclusão às tasks.
2. PR27:contrato principal de QA, testesL06/L07/L08 e D99; observar retificações de contagem, esperadoB07/overlap e o bootstrap do último completo local.
3. PR28:organizações, concorrência/inatividade e foco.
4. PR30:diagnóstico e correções comprovadas de estabilidade, com hipóteses sem prova separadas. O ajuste semanal e a espera unitária do editor também foram transportados a outras propostas.
5. PR32 e PR34:dependências npm/NuGet e suas contraprovas; PR36:SBOM/proveniência depende das duas correções e as incorpora como propostas, com proveniência unsigned.
6. PR31,PR33,PR37,PR38,PR40:chave oculta, ordem do cartão, metodologia, colunas e estado de navegação. Reler os comportamentos combinados nos arquivos compartilhados abaixo.
7. PR35 e PR39:operação Community, recuperação ensaiada e neutralização documental; licença/snapshot público continuam próprios.

## Revisões publicadas nesta conferência

Snapshot 2026-10-20T21:07:20.171242+00:00. Todos os14PRs confirmados em rascunho. SHAs observados antes deste commit documental. Não houve integração.

| PR | SHA |
|---|---|
| [PR27](https://github.com/antoniovitor10/prisma-workspace/pull/27) | e36bbfc |
| [PR28](https://github.com/antoniovitor10/prisma-workspace/pull/28) | 9bad280 |
| [PR29](https://github.com/antoniovitor10/prisma-workspace/pull/29) | ebd761d |
| [PR30](https://github.com/antoniovitor10/prisma-workspace/pull/30) | 62887aa |
| [PR31](https://github.com/antoniovitor10/prisma-workspace/pull/31) | 37c566e |
| [PR32](https://github.com/antoniovitor10/prisma-workspace/pull/32) | 6e261e1 |
| [PR33](https://github.com/antoniovitor10/prisma-workspace/pull/33) | 13b76c5 |
| [PR34](https://github.com/antoniovitor10/prisma-workspace/pull/34) | caa3267 |
| [PR35](https://github.com/antoniovitor10/prisma-workspace/pull/35) | c9afba5 |
| [PR36](https://github.com/antoniovitor10/prisma-workspace/pull/36) | 3bc3363 |
| [PR37](https://github.com/antoniovitor10/prisma-workspace/pull/37) | f567171 |
| [PR38](https://github.com/antoniovitor10/prisma-workspace/pull/38) | 6cb5e90 |
| [PR39](https://github.com/antoniovitor10/prisma-workspace/pull/39) | 52ee3be |
| [PR40](https://github.com/antoniovitor10/prisma-workspace/pull/40) | 3c88673 |

PR27:fonte0a6da1d, CI233PASS/40skips e focal estrito local3PASS; último completo LOCAL anterior232PASS/1bootstrapD06FAIL/40skips, causa sem prova. C4controle posterior27PASS não corrige a causa. PR30:fonte29fd365, CI121PASS/40skips e focal estrito local3PASS; completo local121/40 anterior é de7b39027 e não valida a ordem estrita. A confirmação API agora ocorre depois da UI recarregada nas duas branches. PR40:fonte7188864, frontend182PASS e completo LOCAL123PASS/40skips/0FAIL mais focal5. PR38:completo local123PASS/40skips/0FAIL e focal5. Produtos permanecem independentes, REVISAO/QA oficiais pendentes.

## Arquivos de produto/testes compartilhados

29caminhos comuns, derivados somente dos nomes do diff contraa2797a7. Caminho comum não implica conflito textual; patches idênticos transportados são a mesma correção.

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

NOTAS.md e PROGRESS.md recebem entradas nas propostas e requerem integração cronológica. Apenas o PR36 altera DECISIONS.md e o workflow CI entre as14propostas desta conferência; não há colisão de novas decisões entre branches. A auditoria corrige status canônicos somente com evidência da base; implementação proposta em PR não é conclusão na main.

## Critérios de revisão combinada

- Preservar nome completo obrigatório, erro400 por campo e proteção de segredo do PR27 ao revisar os demais caminhos de criação/edição.
- A chave continua em contratos técnicos; nomes/referências humanas seguem o recorte do PR31. URL não transporta credencial ou conteúdo de tarefa.
- Coluna visual muda Position pelo endpoint existente; classificação/transições/WIP e história mantêm suas regras. A ordem manual de cartões é outro comportamento.
- Pontos aparecem independentemente do valor interno de metodologia, com DTO/enum legados preservados. Filtros/árvore/item/seleção/rolagem usam a URL no backlog.
- Guardas de resposta antiga, rollback e cancelamento continuam necessários quando os consumidores de Kanban/Drawer/Backlog forem lidos juntos.
- Manter teste terminal do editor e privacidade do reporter, distinguir diagnóstico confirmado de hipótese, e preservar erros/execuções anteriores nos relatórios.
- SBOM deve incluir devDependencies na parte frontend, runtime separado e quatro subjects/hashes conferidos; não atribuir assinatura ou nívelSLSA à proveniência unsigned.
- Documentação de recuperação preserva banco/anexos/chaves/configuração; o ensaio de instalação/restauração não equivale a upgrade entre versões nem publicação pública.

As tasks com migration/gate/risco alto, homologações oficiais e dados reais proibidos continuam pendentes conforme auditoria. A varredura final somente leitura consta em relatorios/oficina-varredura.md:15achados, com código, gravidade, reprodução e limites. Último roteiro nativo3PASS/2FAIL em1,5min; falhas no guard por tentativas automáticas bloqueadas de catálogo/Wiki, sem afrouxar a asserção e sem alteração no servidor. Não equivale a homologação oficial nem corrige as lacunas herdadas de horas.
