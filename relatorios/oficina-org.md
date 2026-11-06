# Oficina — ressalvas do PR26, validação em andamento

Base `origin/main@a2797a7`; worktree própria `oficina-org`; branch `fix/org-creation-lows`. Checkpoints `5273e0e` (backend) e `f51aaee` (frontend/testes/documentação). [PR em rascunho #28](https://github.com/antoniovitor10/prisma-workspace/pull/28).

Mudanças em andamento:

- `IOrganizationRepository`/`OrganizationRepository`/`OrganizationsFeature`: elegibilidade considera qualquer membership anterior, inclusive inativa; criação executa leitura, autorização e inserção na mesma transação. SQLServer usa `UPDLOCK,HOLDLOCK` na linha Identity do próprio usuário; `ReadCommitted` evita manter range locks desnecessários de memberships. Sem índice ou migration novos. Lock entre dois POSTs do mesmo usuário; não se alega nova atomicidade com o canal de convite.
- `OrganizationContext`: invalida creation após onboarding e orienta pessoa sem organizações ativas quando o servidor recusa criar outra.
- `NewOrganizationDialog`: usa Radix existente, sem autoFocus nativo antecipando FocusScope; contém foco, fecha com Escape e devolve foco ao menu da conta. Invalida cache de criação. Caller/teste em Topbar atualizados.
- Testes de regras, concorrência SQL e frontend; stub de ProjectFeature atualizado para o contrato aditivo.

Verificação real até aqui:

- Build do backend via testes afetados passou, três avisos anteriores CS8602/xUnit2031.
- xUnit regras de criação e ProjectFeature:17/17PASS. Inclui duas regressões para membership/organização inativa, sem perda da primeira organização de usuário novo nem da autoridade da plataforma.
- Concorrência SQL:3/3PASS, cada caso com3rodadas. Mesmo usuário comum:1criação/2pedidos; plataforma:2/2; usuários distintos:1cada. Banco aleatório com prefixoPrisma_BoardColumns_OrgCreation_, criado e removido pelo próprio teste, exclusivamente SQLQA3111.
- Frontend afetado:13/13PASS. O primeiro teste de foco detectou que autoFocus do input se antecipava ao FocusScope; removido e teste passou. A primeira asserção de cache do onboarding esperava isInvalidated após revalidação ativa; corrigida para exigir nova busca e canCreate:false, comportamento efetivo desejado.
- Contraprova frontend:3FAIL esperadas ao restaurar diálogo anterior e remover invalidação do onboarding; fonte corrigida restaurada por trap absoluto. Um quarto caso ficou fora do filtro, sem skip novo no código.
- Build frontend final PASS (`npm run build`, incluindo `tsc -b`), lint0erros. Build solução verde final:0erros/3avisos anteriores; binários corretos restaurados. FullxUnit:695PASS/33pulos existentes/0falhas (82s); os novos testes SQL de organizações executaram. Os33 dependem de opt-in SQL do método não configurado, sem usar a instância proibida. FullVitest:167PASS/42arquivos/0falhas (208s). E2E e auto-QA ainda pendentes. O typecheck explícito do arquivo E2E também passou; não substitui o gate app tsc-b.
- Contraprova backend:3FAIL esperadas (2inativos e1corrida criando2organizações emvez1),2PASS controles. Fontes corrigidas restauradas; recompilação verde final concluída depois, substituindo os binários temporários da contraprova.

Autorrevisão parcial: nenhum alto/médio confirmado. SQL parametrizado e lock de linha existente cobrem a janela sem membership; transação incluiSave/commit; testes verificam autoridade comum/plataforma e isolamento entre usuários. Slug simultâneo de pessoas diferentes é comportamento anterior fora deste recorte; não alegar solução geral da unicidade de memberships por convite.

Auto-QA: pendente de trava de browser; ainda não executado. Nenhuma aprovação oficial presumida.

Migration: não. Produção, main, deploy, prisma-dev e prisma-metodo intocados. C1 mantém runtime próprio3120/3121 noutra worktree da Oficina.

REVISAO e QA oficiais: pendentes.

## Atualização 07:22UTC — E2E focado e auto-QA

Native organization-creation-lows9/9PASS, incluindo setup e quatro casos por viewport1440x900/390x844. Quatro capturas próprias de diálogo e acesso inativo inspecionadas: campos/botões/orientações alcançáveis, foco visível. FullE2E continua pendente, não é ENTREGA final. API3122 usa o fakeCLI nativo18747 para a suíte completa; nenhum runtime de produção. REVISAO e QA oficiais: pendentes.

## Fechamento local — 20/10/2026

Head de produto e7e8bdd. E2E focal9PASS, quatro capturas1440x900/390x844 examinadas: diálogo com foco contido/retornado e orientação de acesso inativo. Suíte completa registrou111PASS,2FAIL,40skips existentes e14 casos não executados. As duas falhas ocorreram no beforeAll de IA por EADDRINUSE: o provedor falso usava3123, porta da API própria de outra branch Oficina. A configuração privada passou a18747. Também foi preenchido Ai:BridgeToken no runtime próprio, ausente no harness mas obrigatório pelo contrato do fake. Após reiniciar somente API3122 própria, o arquivo IA inteiro passou17/17 em2,1min. Nenhum teste foi pulado nem recebeu retry/prazo/cota extra. Não se apresenta a execução completa anterior como verde: o arquivo afetado foi repetido integralmente após diagnóstico do ambiente.

Confirmação final de tipos: npx tsc -b --force aprovado, evitando reaproveitamento indevido de build-info de node_modules compartilhado. Backend695PASS/33skips existentes e frontend167PASS/42arquivos permanecem os resultados completos do código de produto inalterado. Builds e lint aprovados. Logs da repetição em oficina-qa/.runlogs/oficina/org-ai-fixture-fixed.log.

Autorrevisão concluída no recorte sem alto/médio confirmado. Auto-QA do diálogo/inativo concluído nas duas larguras. Limites: não cobre atomicidade de convites nem unicidade de slug entre usuários distintos. Risco de integração com C5 no mesmo repository deve ser revisado pelo Maestro. Migration: não. REVISAO e QA oficiais: pendentes. PR28 permanece em rascunho.
