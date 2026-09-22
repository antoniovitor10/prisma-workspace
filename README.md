# Prisma WorkSpace

Plataforma open source de gestão ágil para equipes que trabalham com projetos, melhorias e sustentação.

> Repositório público em preparação para a primeira release da Community Edition. A licença de distribuição
> ainda está pendente; a visibilidade pública não substitui uma licença de uso.

## Quickstart local

Com Git, Docker e Docker Compose v2 instalados, baixe o projeto:

```bash
git clone https://github.com/antoniovitor10/prisma-workspace.git
cd prisma-workspace
```

Execute no PowerShell:

```powershell
.\scripts\setup-docker.ps1 -AcceptSqlServerEula
```

No Linux/macOS:

```bash
./scripts/setup-docker.sh --accept-sql-server-eula
```

O ambiente ficará disponível em <http://localhost:8080>. Em uma instalação nova, acesse
<http://localhost:8080/setup> e use o token gerado no `.env` local para criar, uma única vez, a primeira organização
e sua conta administradora. O banco padrão é SQL Server Developer, licenciado somente para desenvolvimento e testes.
Leia o [guia de instalação Docker](docs/installation/docker.md) antes de usar outra edição ou preparar produção.

## Estado atual

- Código-base em .NET, SQL Server e React.
- Snapshot iniciado a partir de uma allowlist, sem importar o histórico institucional anterior.
- Auditoria, rename técnico, instalação Docker independente e setup seguro inicial foram concluídos.
- Backup/restauração, atualização e empacotamento da primeira release ainda estão em preparação.
- A escolha da licença é um gate humano pendente antes da abertura pública.

O plano completo está em [`docs/OPEN-SOURCE-PLAN.md`](docs/OPEN-SOURCE-PLAN.md).

## Projeto e comunidade

- [Como contribuir](CONTRIBUTING.md)
- [Política de segurança](SECURITY.md)
- [Suporte](SUPPORT.md)
- [Governança](GOVERNANCE.md)
- [Integração contínua](docs/maintenance/continuous-integration.md)

A licença ainda é uma decisão humana pendente. Até que um arquivo `LICENSE` seja aprovado, este código não deve
ser tratado como uma distribuição open source licenciada.

## Segurança

Não publique credenciais em issues, commits ou pull requests. A política pública e o canal de reporte serão definidos
em `SECURITY.md` antes do primeiro release candidate.
