# Plano de produto open source do Prisma WorkSpace

## Resultado esperado

Uma Community Edition instalável em ambiente limpo, sem dependência da infraestrutura da Nordevs, com documentação
operacional e comunitária, cadeia de releases verificável e repositório público sem histórico institucional.

## Estratégia de repositórios

- `runrun`: arquivo privado, histórico e referência de recuperação.
- `prisma-workspace`: novo repositório, inicialmente privado, criado a partir de snapshot sanitizado por allowlist.
- O histórico de `runrun` não será importado, reescrito nem publicado.

## Fases e gates de saída

### 0. Preservação e inventário

- Registrar estado Git, remotos, versões e arquivos não consolidados.
- Preservar código, banco e anexos antes de qualquer limpeza.
- Classificar caminhos em `copy`, `review`, `regenerate`, `private-archive` e `deny`.
- Auditar segredos e dados institucionais sem imprimir seus valores.
- **Saída:** fonte recuperável e inventário revisado.

### 1. Contrato da distribuição

- História, spec, decisões, backlog e roadmap rastreáveis.
- Definir Community Edition, plataformas, suporte, licenciamento e política de marca.
- **Saída:** `G-SCOPE` e `G-SPEC`; licença pode continuar pendente apenas enquanto o destino for privado.

### 2. Repositório limpo

- Criar `antoniovitor10/prisma-workspace` privado via Git Bash/GitHub CLI.
- Inicializar histórico novo e copiar somente caminhos aprovados.
- Executar scanner antes de cada push inicial.
- **Saída:** snapshot privado sem conteúdo negado.

### 3. Sanitização e rename técnico

- Remover referências de produto Detran/Runrun e configurações internas.
- Migrar nomes de solution, projetos, namespaces, assemblies, containers e defaults em lotes testáveis.
- Preservar migrations; qualquer baseline depende de `G-MIGRATION`.
- **Saída:** build/testes/E2E verdes e nomenclatura pública consistente.

### 4. Runtimes e banco suportados

- Migrar Node 20 para Node 24 LTS.
- Decidir e registrar eventual migração de .NET 8 para .NET 10 LTS.
- Retirar SQL Server Developer do caminho documentado de produção; suportar Express ou SQL externo licenciado.
- **Saída:** matriz de suporte publicada e imagens sobre runtimes suportados.

### 5. Instalação independente

- Compose sem rede externa privada, URLs fixas ou credenciais distribuídas.
- Healthchecks, readiness, volumes, scripts Bash/PowerShell, doctor, backup e restauração.
- **Saída:** instalação reproduzível em máquina limpa.

### 6. Onboarding seguro

- Primeiro administrador e organização via bootstrap de uso único.
- Workspace demonstrativo opcional e genérico.
- **Saída:** nenhum login padrão e bootstrap fechado após configuração.

### 7. Documentação e comunidade

- Quickstart, configuração, administração, atualização, operação, troubleshooting e desenvolvimento.
- README, LICENSE, CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, SUPPORT, GOVERNANCE e CHANGELOG.
- **Saída:** uma pessoa sem contexto interno consegue instalar e contribuir.

### 8. Segurança e releases

- CI, Dependabot, scanning, SBOM, attestations, checksums, SemVer e release notes.
- Testes de instalação limpa e upgrade.
- **Saída:** `v0.1.0-rc.1` verificável.

### 9. Ensaio e lançamento

- Instalação, uso, reinício, backup, restauração e upgrade em VM limpa.
- Corrigir produto e documentação até todos os gates passarem.
- Escolher licença e aprovar `G-DEPLOY` antes de tornar o repositório público.
- **Saída:** `v0.1.0` público.

## Política de delegação ao Cursor CLI

O Cursor pode executar inventários, renames mecânicos, rascunhos de documentação, scripts e testes em lotes pequenos.
Cada prompt deve declarar objetivo, caminhos permitidos, proibições, critérios de aceite e comandos de verificação.

O Cursor não pode aprovar gates, escolher arquitetura/licença, acessar segredos, publicar, fazer deploy, reescrever
histórico ou usar `--force`/`--yolo`. No Windows, onde seu sandbox nativo não está disponível, tarefas de escrita devem
ocorrer somente no repositório novo ou em worktree isolada e todo diff deve ser revisado antes de integração.

## Definition of Done do lançamento

- Instalação em até três comandos documentados, excluindo downloads.
- Zero segredos e dados institucionais conhecidos no snapshot e histórico público.
- Zero dependências de domínio, rede, container ou VPS da Nordevs.
- Runtime suportado e uso do SQL Server compatível com licença.
- Build, testes, E2E, instalação limpa, backup/restauração e upgrade aprovados.
- Documentação e arquivos de comunidade completos.
- Licença e política de marca aprovadas pelo PO.
