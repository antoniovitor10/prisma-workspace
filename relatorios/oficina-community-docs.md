# Oficina — operação Community e referências institucionais (recorte TASK500)

Base origin/main a2797a71b79543cf71930d04547021564a7e4e88. Branch docs/community-neutral-operations. Spec aprovada open-source-distribution, história US-OPEN-SOURCE-001, D68/D75/D76/D77. Somente documentação.

O guia docs/installation/deployment.md apresentava host, diretórios, rede, container e banco de uma instalação específica. Agora descreve operação independente, configuração externa, persistência conjunta de banco/anexos/chaves/configuração, validação de candidata e recuperação. Não modifica o CI ou implementa atualização automática. LEGACY-REFERENCES.md recebe inventário delimitado, separado das contagens históricas. README e seed já são neutros; nenhuma troca artificial nesses arquivos.

Verificação real:git diff --check e link local para docker.md aprovados. Quatro classes concretas de referências operacionais aparecem na base e zero no novo guia; contraprova usa git show da base, sem modificar instalação. Scanner gitleaks não está instalado: nenhum scanner global de segredos foi executado. Não ler .env ou method-git.env, dados privados, conversas ou produção. Builds/E2E não repetidos pela exceção de documentação sem impacto no comportamento.

Autorrevisão:sem achado alto no recorte; nenhum comando operacional foi executado. O guia mantém aprovação específica de schema e recuperação e não pressupõe reversibilidade de migrations. Auto-QA documental:links e coerência dos passos conferidos; nenhum novo comportamento de UI. Migration:não.

TASK500 permanece parcial:compatibilidade de storage, IDs/classes de migrations e histórico técnico preservados; documentos de ambiente/desenvolvimento e histórico inteiro exigem seleção/anonimização própria antes de snapshot público. Não é certificação de ausência de segredos ou aprovação para publicar. Licença e G-DEPLOY continuam pendentes; não criar release/tag nem promover repositório. REVISAO e QA oficiais:pendentes.
