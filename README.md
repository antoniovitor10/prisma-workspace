# Prisma WorkSpace

Plataforma open source de gestão ágil para equipes que trabalham com projetos, melhorias e sustentação.

> Este repositório está em preparação privada para a primeira distribuição da Community Edition. A instalação
> pública ainda não foi liberada; os passos atuais podem depender do ambiente de desenvolvimento.

## Quickstart local

Com Docker e Docker Compose v2 instalados, execute no PowerShell:

```powershell
.\scripts\setup-docker.ps1 -AcceptSqlServerEula
```

No Linux/macOS:

```bash
./scripts/setup-docker.sh --accept-sql-server-eula
```

O ambiente ficará disponível em <http://localhost:8080>. O banco padrão é SQL Server Developer, licenciado somente
para desenvolvimento e testes. Leia o [guia de instalação Docker](docs/installation/docker.md) antes de usar outra
edição ou preparar produção.

## Estado atual

- Código-base em .NET, SQL Server e React.
- Snapshot iniciado a partir de uma allowlist, sem importar o histórico institucional anterior.
- Auditoria e rename técnico foram concluídos; instalação independente e documentação estão em execução.
- A escolha da licença é um gate humano pendente antes da abertura pública.

O plano completo está em [`docs/OPEN-SOURCE-PLAN.md`](docs/OPEN-SOURCE-PLAN.md).

## Projeto e comunidade

- [Como contribuir](CONTRIBUTING.md)
- [Política de segurança](SECURITY.md)
- [Suporte](SUPPORT.md)
- [Governança](GOVERNANCE.md)
- [Integração contínua](docs/maintenance/continuous-integration.md)

A licença ainda é uma decisão humana pendente. Até que um arquivo `LICENSE` seja aprovado e o repositório seja tornado
público, este código não deve ser tratado como uma distribuição open source publicada.

## Segurança

Não publique credenciais em issues, commits ou pull requests. A política pública e o canal de reporte serão definidos
em `SECURITY.md` antes do primeiro release candidate.
