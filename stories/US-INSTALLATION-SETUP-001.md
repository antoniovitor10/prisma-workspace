# US-INSTALLATION-SETUP-001: Configurar a primeira administração de uma instalação Community

**Status:** active
**Módulo:** installation-setup
**Spec vinculada:** `SPEC-INSTALLATION-SETUP`

## História

**Como** pessoa responsável por uma instalação nova do Prisma WorkSpace Community
**Quero** concluir uma configuração inicial segura e única com minha organização e conta administradora
**Para** começar a usar a instância sem credenciais distribuídas, usuários fictícios ou dados demonstrativos

## Cenários esperados

- **Dado** uma instalação nova, sem administrador ativo, com setup explicitamente habilitado e token externo válido
  **Quando** informo nome, e-mail, senha, organização e slug válidos
  **Então** a instalação cria uma única conta administradora, sua organização e membership Administrator
  como uma operação atômica, sem criar conteúdo demonstrativo

- **Dado** que o setup inicial já foi concluído
  **Quando** uma pessoa consulta ou tenta executar o setup
  **Então** o setup permanece indisponível de forma permanente e nenhuma segunda administração inicial é criada

- **Dado** duas tentativas concorrentes de configuração inicial
  **Quando** ambas chegam à instância ainda não inicializada
  **Então** somente uma pode concluir e a outra recebe resultado controlado, sem criar usuários ou organizações extras

## Observações humanas

Esta história deriva o onboarding previsto na `SPEC-OPEN-SOURCE-DISTRIBUTION`. A confirmação automática do e-mail
da primeira conta, o nome e a forma final das configurações externas e a experiência guiada de primeiro acesso
dependem de aprovação humana na `SPEC-INSTALLATION-SETUP`. Nenhuma implementação é autorizada antes do `G-SPEC`.
