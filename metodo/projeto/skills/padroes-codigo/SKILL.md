---
name: padroes-codigo
description: Convenções de código do Prisma WorkSpace - nomes de entidades e DTOs, um Command ou Query com Handler por caso de uso, controller sem regra, validação, erros, async, idioma, componentes React com Styled Components e TanStack Query, padrão de commits. Use em qualquer task que crie ou altere código C# ou TypeScript.
metadata:
  origem: projeto
  versao: "1"
---

# Padrões de código

## Regras

- **Entidades** em PascalCase singular: `Board`, `Stage`, `WorkItem`, `TimeEntry` (`src/Prisma.Workspace.Domain/Entities/`). A tarefa é `WorkItem`, nunca `Task`, para não colidir com `System.Threading.Tasks.Task`.
- **DTOs** com sufixo `Dto`: `CreateWorkItemDto`, `WorkItemDetailDto`, `StageDto`.
- **Um caso de uso = um Command ou Query + um Handler**, numa pasta por área: `Application/Features/<Area>/Commands` e `Queries`. O command é um `record` que implementa `IRequest<T>`.
- **Validação** com FluentValidation, um `<Command>Validator` ao lado do command, mensagens em pt-BR. O `ValidationBehavior` roda os validators antes do handler; não chame o validator à mão.
- **Controller sem regra de negócio** (princípio 10): monta o command com o `UserId` das claims e chama `_mediator.Send`. Sem `try/catch`: o `MapeamentoDeErrosMiddleware` traduz as exceções.
- **Erros:** regra de domínio quebrada usa `DomainException.Garantir(condicao, "mensagem em pt-BR")` (vira 400); não encontrado usa `NaoEncontradoException` (404); sem permissão, `AcessoNegadoException`. Mensagens para o usuário em pt-BR.
- **Permissão e tenant no handler**, antes de qualquer escrita: `IPermissionService.EnsureAsync` e `IProjectAccessService.EnsureAtLeastAsync` (princípio 12).
- **Tudo que toca banco é `async`**, com sufixo `Async` e `CancellationToken` repassado.
- **Idioma:** código e identificadores em inglês; comentários, XML doc e documentos em pt-BR (princípio 15). Há nomes antigos em português no código (`Garantir`, `MapeamentoDeErrosMiddleware`, `NaoEncontradoException`): mantenha, não renomeie em task que não seja sobre isso.
- **Frontend:** componente funcional em `.tsx`, estilo com Styled Components lendo `theme` (nunca cor ou tamanho literal), dados do servidor com TanStack Query (`useQuery` com `queryKey` em array) chamando o cliente único `src/services/api.ts`. Sem emoji na interface.
- **Commits:** Conventional Commits em inglês, imperativo e minúsculo. Nas tasks do método: `{tipo}(T-XXXX): {resumo}`, branch `task/T-XXXX-slug`, título do PR `[T-XXXX] título` (o webhook do Prisma lê os dois).

## Exemplos do projeto

- `src/Prisma.Workspace.Application/Features/Stages/Commands/CreateStageCommand.cs:9-17`: command como `record` com `IRequest<Guid>`.
- `src/Prisma.Workspace.Application/Features/Stages/Commands/CreateStageCommandValidator.cs:8-23`: validator com mensagens em pt-BR.
- `src/Prisma.Workspace.Application/Features/Boards/Commands/CreateBoardCommandHandler.cs:33-53`: handler que checa permissão e acesso ao projeto antes de criar e falha com mensagem clara.
- `src/Prisma.Workspace.Api/Controllers/StagesController.cs:17-39,76-88`: controller `[Authorize]`, `UserId` das claims e só `_mediator.Send`.
- `src/Prisma.Workspace.Api/Middleware/MapeamentoDeErrosMiddleware.cs:9-13`: tradução de exceções em respostas HTTP.
- `src/Prisma.Workspace.Domain/Exceptions/DomainException.cs:16`: `Garantir`.
- `src/Prisma.Workspace.Web/src/layout/Topbar.tsx:33-50`: Styled Components lendo `theme.color`, `theme.font`, `theme.radius`.
- `src/Prisma.Workspace.Web/src/layout/Topbar.tsx:489-494`: `useQuery` com `queryKey` em array e `api.getOrganizationCreation()`.

## Armadilhas conhecidas

- Alguns handlers recebem dependências opcionais (`IPermissionService? permissions = null`, `CreateBoardCommandHandler.cs:20-24`) por compatibilidade com testes antigos. Em handler novo, injete como obrigatória.
- `src/services/api.ts` tem mais de 1.300 linhas e usa `credentials: 'include'` (cookie de refresh). Acrescente métodos nele; não crie outro cliente HTTP.
- Migrations têm regra própria (skill `dados`).

## Origem das regras

- `AGENTS.md` antigo (Convenções), D7, D39, D68, D90.
- Arquivos citados acima; histórico do git (`git log --oneline`).
