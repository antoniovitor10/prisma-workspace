# Prisma — workspace principal

Atualizado em 2026-10-05. Responsável pelo escopo: Vitor (PO). Coordenação: Maestri.

## Objetivo e situação
Reunir produto, landing, planejamento e memória em um monorepo e eliminar cópias locais concorrentes.
Base canônica: `antoniovitor10/prisma-workspace`, main `9d48f1df612c4aabce76fd2bbe7317fcdb64e702`.
Branch de organização: `chore/maestri-workspace`; nenhuma alteração foi publicada.
O produto é a plataforma .NET 8/EF Core/SQL Server/React 19, não Prisma ORM.

## Repositórios e ambientes
- Aplicação: `src/Prisma.Workspace.*`; backend xUnit em `tests/`; landing em `apps/landing`.
- Remoto único: `https://github.com/antoniovitor10/prisma-workspace.git`.
- Produção documentada: `https://prisma.nordevs.com.br`; última evidência documental de deploy em 2026-09-22.
- Landing documentada: `https://antoniovitordev.com.br/prisma/`, `/prisma/v2/`, `/prisma/v3/`.
- Desenvolvimento: `npm run dev` na raiz, conforme `docs/installation/development.md`; Docker em localhost:8080.
- Context Explorer: localhost:5174 e API localhost:3847. Não confundir com o produto.
- Credenciais: `.env` local, configuração de desenvolvimento/E2E, user-secrets e variáveis do servidor.
  Configurações antigas estão arquivadas privadamente; validar acessos de SQL Server, SMTP, GitHub e hospedagem
  antes de usar. Nenhum valor de credencial consta neste painel.

## Prioridades e backlog atual
Esta tabela ordena frentes; não duplica nem conclui automaticamente as 108 tarefas históricas de `backlog.md`.

| Frente / andar | Componentes | Próximo resultado verificável |
| --- | --- | --- |
| Base e governança | Monorepo, docs e ferramentas | Auditoria pública e testes de governança aprovados; licença definida pelo PO antes da release |
| Confiabilidade do produto | API, Web, SQL Server, testes | Comparar pendências recuperadas com main e abrir somente correções ainda necessárias com fonte e aceite |
| Assistente IA — fundação | API, infraestrutura, Web | Revisar SPEC-AI-001 draft e obter G-SPEC; nenhum código de IA liberado por esta organização |
| Community e lançamento | Produto, instalação e apps/landing | Checklist de release, backup/restauração e mensagens da landing consistentes com o produto |
| Operação e integrações | Publicação, SQL Server, SMTP e integrações | Inventário de acessos/ambientes validado sem expor segredos e sem alterar produção |

Decisões vigentes: D89 (colunas independentes), D90 (acesso prévio à atribuição), D91 (assistente IA, antiga
D90 de 24/09) e D92 (organização local). ROADMAP/PROGRESS/backlog contêm checkpoints antigos; cruzar sempre
com as entradas mais recentes e com o código. Não reabrir N:N ou reimplementar entregas já publicadas por ler
um checkpoint antigo isoladamente.

## Equipe por demanda
Reutilizar Planejador (terminal existente) como PM, planejamento técnico e consolidação do contexto.
Executor trabalha em mudanças pequenas com isolamento próprio. Revisor acumula segurança básica e compatibilidade.
QA acumula UX/acessibilidade/mobile. Dados/integrações e infraestrutura entram como especialistas na entrega que
precisar deles; design e segurança especializados entram quando a complexidade exigir. Não há contratação automática
nem agente novo iniciado. Contratos YAML em `agents/` são documentos, não agentes ativos no Maestri.

## Isolamento e acompanhamento
Workspace Prisma: `869973fe-1587-48b0-a252-bf65709fd457`. Ground concentra visão, prioridades e decisões.
Andares de planejamento usam `--no-git` e compartilham a raiz. Não executar trabalho simultâneo neles.
O Maestri suporta worktree Git por andar, clone isolado e status; worktrees nativos ficam em `.maestri/floors`
ao lado do projeto. Ativar isolamento apenas na entrega executável, evitando criar novamente cópias antecipadas.
Um único Git contém todos os componentes: entrega transversal usa um andar e uma branch/worktree.

Cada andar contém duas notas visuais, verificadas no canvas, e um espelho versionado:
- [Base e governança](floors/base-governanca.md)
- [Confiabilidade do produto](floors/confiabilidade-produto.md)
- [Assistente IA — fundação](floors/assistente-ia.md)
- [Community e lançamento](floors/community-lancamento.md)
- [Operação e integrações](floors/operacao-integracoes.md)

Para manter o trabalho na mesma pasta, worktrees também podem ser criadas em `.local/worktrees/<entrega>`.
O terminal pode ser posicionado no andar com `maestri recruit --floor ... --dir ...`; a CLI e a skill instalada
confirmam que `--dir` prevalece sobre o diretório padrão do andar. O diretório deve existir antes do comando.
As notas visuais dos andares ficaram sem terminais permanentes; o espelho acima garante que agentes futuros
tenham seu contexto mesmo antes de conectar notas no canvas. Ground mantém as quatro notas conectadas ao Planejador.

## Pendências preservadas e dúvidas
- Recuperação privada: `.local/recovery/20261005`, ZIPs verificados, patches de índice/working tree e Git bundles.
- Branches antigas do Prisma preservadas como `refs/archive/prisma/*` e `refs/archive/cleanup/*`, sem trazer histórico Runrun
  para o repositório público. Pendências locais de UI, QA e migração não são aplicadas cegamente sobre main.
- Runrun era a origem histórica; documentos institucionais e arquivos de migração não são materiais públicos de release.
- Definir licença e critérios de release. Conferir quais pendências recuperadas continuam necessárias.
- Não regenerar `stories/catalog.json` cegamente: `materialize-stories.js` reconhece documentos avulsos
  nomeados US-..., mas não o agrupamento `stories/ai-assistant.md`; preservar a intenção humana e a spec draft.
- A renumeração D91 reconcilia a colisão de D90; o conteúdo original permanece no arquivo privado e na memória histórica.

## Fontes e período
Inventário de arquivos, Git e notas do projeto até 2026-10-05; registros documentais relevantes de setembro/2026.
Fonte da demanda de organização: Vitor, conversa atual, 2026-10-05, decisão D92.
O ambiente não forneceu o identificador da conversa; a referência disponível é o pedido desta sessão.
Fonte do assistente: registro local do pedido do PO em 2026-09-24, `stories/ai-assistant.md`, D91, memória histórica.
Não foi lido nenhum histórico de WhatsApp ou de conversas externas. Nomes/IDs de conversas devem ser conferidos antes.
Conversas não confirmadas não geram tarefas. Cada demanda futura registra fonte, autor, data, referência original,
componente e eventual vínculo incerto; comparar IDs e conteúdo com `backlog.md` antes de abrir uma tarefa.
Cursor recorrente: não inicializado; sem conversa confirmada/lida, não há ponto de leitura a avançar.

## Última entrega e próximo passo
Última entrega: consolidação local, landing incorporada, pendências protegidas e cópias redundantes removidas.
Próximo passo: escolher uma frente e revisar suas pendências/aceites antes de começar implementação.
Limitação: Orca está fechado, e a CLI não disponibiliza remoção de repositório; metadados globais do aplicativo
não foram editados. Diretórios locais antigos ligados a Runrun foram removidos após recuperação verificada.
