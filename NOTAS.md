# Continuidade — tarefa 6, parte A

Atualizado em 2026-10-14 por Tear.

- Revisão L06 em test/e2e-l07: casos divididos, recusas API 400 comprovadas, nenhum PUT de UI sem consentimento, editor novo para distinguir segundo erro. Reabertura UI e descendentes externos API separados; sem produto/spec/schema.
- Focal final 9/9 desktop/mobile, 82,43 s, máximo 15,57 s; tsc -b/lint/tipos E2E aprovados. Diagnóstico anterior 60/61, timeout por resposta antiga em reload, e 20,30 s mobile motivando divisão. Relatório docs/e2e-l06-review-validation.md.
- Próximo passo da mesma tarefa: criar test/e2e-l08 deste commit e implementar horas com usuário/projeto próprios; repetir L06+L08 três vezes, recriar somente banco prisma-dev e suíte completa. Não alterar rich-task-description antigo; registrar se reaparecer.
- QA prisma-qa em 3110/SQL3111/PrismaWorkspace_QA permanece separada; login privado .env.qa.login modo 600, nunca imprimir senha. Dev em 3100/SQL3102/DetranKanban_E2E. Sem produção/main/deploy/PR/agentes.
