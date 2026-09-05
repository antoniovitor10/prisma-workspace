# US-OPEN-SOURCE-001: Instalar e contribuir com o Prisma WorkSpace

**Status:** active
**Módulo:** distribuição-open-source
**Spec vinculada:** `SPEC-OPEN-SOURCE-DISTRIBUTION`

## História

**Como** pessoa ou organização interessada no Prisma WorkSpace
**Quero** instalar, atualizar, avaliar e contribuir com o produto usando somente o repositório público
**Para** operar a Community Edition sem depender da infraestrutura, dos dados ou do conhecimento interno da Nordevs

## Cenários esperados

- **Dado** um ambiente limpo com Docker disponível
  **Quando** a pessoa segue o guia de início rápido
  **Então** consegue iniciar uma instalação funcional com configuração própria e sem credenciais predefinidas

- **Dado** o repositório que originou o produto
  **Quando** o snapshot público é preparado
  **Então** dados institucionais, segredos, artefatos gerados e histórico privado não são publicados

- **Dado** que uma nova versão é publicada
  **Quando** uma instalação suportada é atualizada
  **Então** existe documentação, backup e caminho de rollback verificáveis

## Observações humanas

O PO aprovou em 2026-09-05 a execução do plano de produto open source em fases, a criação de um novo repositório
via Git Bash e o uso do Cursor CLI como executor de tarefas delimitadas sob revisão do Codex. A escolha da licença
permanece pendente e bloqueia tornar o repositório público, mas não bloqueia sua preparação privada.
