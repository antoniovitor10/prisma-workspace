# Validação da organização — 2026-10-05

- Context Explorer: 42 testes aprovados, nenhum erro.
- Loop de execução: 47 testes aprovados, nenhum erro.
- Catálogo: 48 specs, 108 tarefas históricas e 347 histórias; 73 referências do backlog normalizadas com
  `spec_reference_original` preservado. Tarefas com múltiplos contratos mantêm a referência completa nessa
  propriedade; o campo `spec` identifica o contrato principal para leitura pelo Context Explorer.
- Quatro specs receberam título e nome padronizado da seção de aceite, mantendo contratos e status.
- Catálogo anterior preservado: 30 histórias derivadas adicionadas para specs sem cobertura, mais as três
  histórias humanas da fundação IA. SPEC-AI-001 continua draft; histórias de fatias futuras permanecem no documento.
- Landing: 18 arquivos preservados; nove referências locais de HTML verificadas, nenhum arquivo ausente.
- Scanner de segredos sobre a árvore preparada: 8,16 MB lidos, nenhum achado, sem incluir recuperação privada.
- Auditoria na raiz consolidada aprovada; arquivos privados ignorados e nenhuma cópia Git aninhada da landing.
- `git diff --check` aprovado na árvore preparada.
- Recuperação: 27 origens, 22.371 arquivos, cinco bundles verificados. Revalidação das origens obrigatória
  imediatamente antes da remoção; resultado detalhado fica no manifesto privado e em `cleanup-result.json`.
- 21 referências antigas do Prisma arquivadas em `refs/archive/`; não são branches de trabalho nem são
  incluídas automaticamente em `git push --all`. Histórico Runrun permanece somente nos bundles privados.
- Cinco andares com duas notas cada, conteúdo lido e espelhado em `docs/workspace/floors`.
  Os cinco terminais temporários executaram somente PowerShell/exit e foram removidos; não iniciaram IA.
- Sem alterações no código funcional de API/React, schema ou publicação. E2E não foi executado porque esta
  entrega trata de documentação, localização de arquivos e configuração interna, conforme exceções do AGENTS.md.

Falhas encontradas e resolvidas: contagens antigas, specs sem título reconhecível, falta de histórias no catálogo
e referências incompletas no backlog. A máquina também ficou sem memória ao executar verificações simultâneas;
os testes passaram em série com `--test-concurrency=1`, heap de 128 MB e semi-space de 1 MB.
A recuperação inicial foi retomada em streaming após falta de memória; nenhuma origem foi removida nessa falha.

Limitação operacional: aplicativo anterior fechado e sem comando CLI para remover cadastro de repositório.
Metadados globais e hooks não foram modificados. A limpeza autorizada remove os diretórios físicos ligados ao projeto.
