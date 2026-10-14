# Continuidade — tarefa 6, parte A

Atualizado em 2026-10-14 por Tear.

- Revisão L06 em test/e2e-l07: casos divididos, recusas API 400 comprovadas, nenhum PUT de UI sem consentimento, editor novo para distinguir segundo erro. Reabertura UI e descendentes externos API separados; sem produto/spec/schema.
- Focal final 9/9 desktop/mobile, 82,43 s, máximo 15,57 s; tsc -b/lint/tipos E2E aprovados. Diagnóstico anterior 60/61, timeout por resposta antiga em reload, e 20,30 s mobile motivando divisão. Relatório docs/e2e-l06-review-validation.md.
- Próximo passo da mesma tarefa: criar test/e2e-l08 deste commit e implementar horas com usuário/projeto próprios; repetir L06+L08 três vezes, recriar somente banco prisma-dev e suíte completa. Não alterar rich-task-description antigo; registrar se reaparecer.
- QA prisma-qa em 3110/SQL3111/PrismaWorkspace_QA permanece separada; login privado .env.qa.login modo 600, nunca imprimir senha. Dev em 3100/SQL3102/DetranKanban_E2E. Sem produção/main/deploy/PR/agentes.

- Complemento A: conclusão UI e recursão profunda API separadas, payload de consentimento verdadeiro comprovado, todas as recargas/navegações L06 aguardam HTTP. Spec budget 20 s. Focal final 25/25 / 164,98 s, máximo 10,51 s. Banco histórico: GET Boards 446 em 4,01 s, 433 de 215 projetos arquivados; rodada conjunta diagnóstica interrompida (32 normais, 3 esperadas L08, 5 erros de carga, 1 interrupção, 401,15 s). Sem correção de produto. Reset inicial somente dev 40,67 s / 28 migrations / 1 seed. Repetição final e suíte limpa ainda seguem nesta tarefa B.

- Barreira final de navegação: requestfinished + timing posterior aos headers do documento novo; evita refetch cancelado antigo, mantém GET 200 obrigatório. Fixture functional.ts opt-in L06/L08 com Google Fonts CSS vazio no headless; API/ativos locais reais, produto/QA intactos. Diagnóstico 31 reproduções sem pageerror, não comprovado crash. Focal 19/19, três vezes por dispositivo. Ainda repetir todos os specs finais e suíte limpa na branch B.
