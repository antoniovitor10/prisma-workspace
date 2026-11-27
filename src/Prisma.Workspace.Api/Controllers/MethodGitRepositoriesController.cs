using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method/repositories")]
[TypeFilter(typeof(MethodGitEndpointFilter))]
public sealed class MethodGitRepositoriesController(IMediator mediator) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
        => Ok(await mediator.Send(new ListMethodGitRepositoriesQuery(projectId, ActorId), ct));
    [HttpPost]
    public async Task<IActionResult> Register(Guid projectId, RegisterGitRepositoryRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterMethodGitRepositoryCommand(projectId, request, ActorId), ct));
    /// <summary>Cadastro no modo consulta (sem App nem webhook), com o token de leitura da organização.</summary>
    [HttpPost("poll")]
    public async Task<IActionResult> RegisterPoll(Guid projectId, RegisterPollGitRepositoryRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterPollMethodGitRepositoryCommand(projectId, request, ActorId), ct));
    /// <summary>"Consultar agora" de um repositório no modo consulta (uma por minuto).</summary>
    [HttpPost("{integrationId:guid}/poll")]
    public async Task<IActionResult> Poll(Guid projectId, Guid integrationId, CancellationToken ct)
        => Ok(await mediator.Send(new PollMethodGitRepositoryCommand(projectId, integrationId, ActorId), ct));
    [HttpPut("{integrationId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid integrationId, UpdateGitRepositoryRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateMethodGitRepositoryCommand(projectId, integrationId, request, ActorId), ct));
}

/// <summary>Habilitação global explícita; erros operacionais não expõem resposta/credencial do provedor.</summary>
public sealed class MethodGitEndpointFilter(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!configuration.GetValue<bool>("MethodGit:Enabled"))
        {
            context.Result = Problem(503, "method_git_disabled", "Integração Git do método desabilitada nesta instalação.");
            return;
        }
        try
        {
            var executed = await next();
            if (executed.Exception is MethodOperationException method)
            { if (method.StatusCode == 429) context.HttpContext.Response.Headers.RetryAfter = "60";
                executed.Result = Problem(method.StatusCode, method.Code, method.Message); executed.ExceptionHandled = true; }
            else if (executed.Exception is GitHubUnavailableException github)
            { executed.Result = GitHubProblem(github); executed.ExceptionHandled = true; }
        }
        catch (MethodOperationException method)
        { if (method.StatusCode == 429) context.HttpContext.Response.Headers.RetryAfter = "60";
            context.Result = Problem(method.StatusCode, method.Code, method.Message); }
        catch (GitHubUnavailableException github) { context.Result = GitHubProblem(github); }
    }
    // 404 do GitHub (PR, branch ou repositório inexistente para a credencial) tem mensagem própria; o resto segue como indisponível.
    // 401 e 403 (fora do limite de consultas) vêm do token de leitura do modo consulta: mensagem própria, nunca o texto do GitHub.
    private static ObjectResult GitHubProblem(GitHubUnavailableException github) => github switch
    {
        { Code: "git_not_found" } => Problem(404, github.Code, "Não encontrado no GitHub: confira o repositório e o número do PR ou o nome da branch."),
        { Code: "git_credential_revoked" } => Problem(400, github.Code, "O token de leitura desta organização foi revogado no Prisma. Cadastre um novo token."),
        { StatusCode: 401 } => Problem(400, "git_token_invalid", "O GitHub recusou o token de leitura: ele está inválido, vencido ou foi revogado."),
        { StatusCode: 403, Code: not "git_rate_limited" } => Problem(400, "git_token_forbidden", "O GitHub recusou o acesso: falta a aprovação do token pela organização ou a leitura deste repositório."),
        _ => Problem(503, github.Code, "GitHub indisponível ou sem acesso completo. Avise o administrador."),
    };
    private static ObjectResult Problem(int status, string code, string message)
    {
        var problem = new ProblemDetails { Status = status, Title = message };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
