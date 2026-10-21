# SPEC-OPEN-SOURCE-DISTRIBUTION: Distribuição e instalação da Community Edition

**Status:** approved

## Objective

Transformar o estado atual do Prisma WorkSpace em uma distribuição open source reproduzível, documentada e segura,
preservando o repositório existente como arquivo privado e iniciando o repositório público a partir de um snapshot
sanitizado com histórico novo.

## Context

O repositório atual nasceu de um projeto institucional, contém referências legadas, documentos de entrada, scripts
de migração específicos, configurações de produção e uma árvore de trabalho com mudanças não consolidadas. Copiar seu
histórico para um repositório público pode expor conteúdo que não aparece mais no estado final.

## Scope

- Criar `antoniovitor10/prisma-workspace` inicialmente privado.
- Preparar snapshot por allowlist, sem fork, clone do histórico ou history rewrite do repositório atual.
- Preservar código de produto, testes e migrations aplicadas até uma estratégia de baseline aprovada.
- Remover do snapshot público documentos institucionais, dados reais, caches, logs, saídas e configurações privadas.
- Tornar domínio, credenciais, URLs, banco e infraestrutura configuráveis.
- Oferecer instalação Docker independente, healthchecks, persistência, backup, restauração e diagnóstico.
- Criar onboarding do primeiro administrador sem credenciais distribuídas.
- Criar documentação de usuário, administração, operação, contribuição, segurança e releases.
- Automatizar build, testes, instalação limpa, análise de dependências, SBOM e proveniência de releases.
- Usar Cursor CLI somente em tarefas delimitadas, sem autoaprovação de gates e sob revisão humana/Codex.

## Out of Scope

- Publicar o repositório antes da decisão de licença e da auditoria de segurança.
- Apagar, reescrever ou tornar público o histórico do repositório `runrun`.
- Remover migrations aplicadas sem `G-MIGRATION` e plano de compatibilidade.
- Alterar funcionalidades de gestão não necessárias à distribuição.
- Dar ao Cursor CLI acesso autônomo a segredos, produção, gates, publicação ou comandos destrutivos.

## Functional Requirements

1. Uma instalação limpa deve depender apenas de ferramentas e parâmetros documentados.
2. Nenhum segredo, dado pessoal, domínio interno ou credencial real pode integrar o snapshot público.
3. Configuração local deve partir de exemplos sem valores sensíveis e permanecer fora do Git.
4. O Compose não pode depender de redes, containers ou reverse proxies privados da Nordevs.
5. SQL Server Developer não pode ser apresentado como banco de produção; a edição/opção suportada deve respeitar sua licença.
6. O primeiro usuário administrador deve ser criado por bootstrap de uso único ou fluxo equivalente seguro.
7. Releases devem seguir SemVer e publicar notas, imagens identificáveis, checksums, SBOM e proveniência quando disponíveis.
8. Instalação, atualização, backup, restauração e rollback devem ser ensaiados em ambiente limpo.
9. O repositório público deve possuir README, licença, contribuição, código de conduta, segurança, suporte e governança.
10. Cada lote delegado ao Cursor deve declarar caminhos permitidos, proibições, critérios de aceite e comandos de teste.

## Invariants

- O repositório atual permanece arquivo privado e recuperável.
- Human Gates só podem ser aprovados pelo PO.
- Segredos permanecem em variáveis externas ou cofre.
- O snapshot público começa com histórico limpo.
- Migrations existentes não são lixo e não são descartadas por limpeza mecânica.
- Mudanças funcionais continuam seguindo história, spec, testes e gates do projeto.

## User Story References

- `US-OPEN-SOURCE-001`

## Acceptance Criteria

- **Given** o repositório privado atual
  **When** a base pública é criada
  **Then** ela possui histórico próprio, começa privada e não contém arquivos negados pelo inventário

- **Given** uma máquina limpa suportada
  **When** o quickstart documentado é executado
  **Then** API, frontend e banco ficam healthy sem dependência da rede privada da Nordevs

- **Given** uma varredura do snapshot e de seu histórico
  **When** a promoção pública é solicitada
  **Then** não existem segredos ou dados institucionais conhecidos e a licença já foi escolhida pelo PO

- **Given** um lote produzido pelo Cursor CLI
  **When** ele é candidato a integração
  **Then** seu diff e seus testes são revisados antes de commit, push ou deploy

## Data Impact

Não há alteração de schema nesta primeira etapa. Onboarding, baseline de migrations ou mudança da edição/configuração
do banco exigirão revisão desta spec e `G-MIGRATION` quando afetarem dados persistidos.

## Authorization Impact

O futuro bootstrap deve impedir criação adicional de administradores após a configuração inicial e nunca pode
contornar o isolamento multitenant ou as regras atuais de autorização.

## Contracts

Nenhum contrato de API muda na fase de inventário e criação privada. Health/readiness e bootstrap serão contratados
em specs derivadas antes de alterar a API.

## Dependencies

- D68, D74 e D75.
- Escolha humana da licença antes da promoção pública.
- Estratégia aprovada para runtime e SQL Server antes do release estável.

## Error Cases

- Segredo detectado: bloquear cópia/push e rotacionar a credencial quando aplicável.
- Referência institucional ambígua: classificar para revisão humana, sem publicar.
- Teste ou instalação limpa falha: manter a fase aberta e corrigir antes da promoção.
- Licença ausente: manter o repositório privado.

## Pending Decisions

- Licença: AGPL-3.0 com eventual licença comercial, ou Apache-2.0.
- Política de marca e uso do nome Prisma WorkSpace.
- Suporte oficial inicial: Linux/Docker e eventual escopo adicional para Windows/macOS.
- Cronograma de migração de .NET 8 para .NET 10 LTS.

## Test Gate Mapping

- Inventário: testes de allowlist/denylist e scanner de segredos.
- Distribuição: build backend/frontend, testes unitários e E2E.
- Instalação: smoke em ambiente limpo e healthchecks.
- Operação: ensaio de backup, restauração, atualização e rollback.

## Risks

- Exposição de conteúdo removido se o histórico antigo for reutilizado.
- Remoção acidental de migrations ou código ainda necessário.
- Imagem de banco incompatível com uso comercial/produção.
- Documentação funcionar apenas na máquina dos mantenedores.
- Mudanças amplas do Cursor sem contexto ou revisão suficiente.

## Rollback

O repositório atual não será alterado destrutivamente. O novo repositório pode permanecer privado ou ser arquivado;
o checkpoint privado continua sendo a fonte recuperável durante toda a preparação.

## Human Gates

- `G-SCOPE`: aprovado pelo PO em 2026-09-05 para execução do plano em fases.
- `G-SPEC`: aprovado pelo PO em 2026-09-05 ao autorizar a execução do plano apresentado.
- `G-MIGRATION`: pendente e condicional.
- `G-DEPLOY`: pendente para publicação de imagens e promoção pública.
- `G-SCOPE-LICENSE`: pendente; bloqueia tornar o repositório público.

## Traceability

US-OPEN-SOURCE-001 → SPEC-OPEN-SOURCE-DISTRIBUTION → TASK-041 → inventário → snapshot privado → gates → release
