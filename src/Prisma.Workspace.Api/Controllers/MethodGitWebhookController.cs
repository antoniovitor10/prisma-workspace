using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Infrastructure.Services.MethodGit;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/integrations/git/github/{integrationId:guid}/webhook")]
[TypeFilter(typeof(MethodGitEndpointFilter))]
[TypeFilter(typeof(MethodGitWebhookRateFilter))]
public sealed class MethodGitWebhookController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(GitWebhookIngress.MaximumBodyBytes + 1)]
    public async Task<IActionResult> Receive(Guid integrationId, CancellationToken ct)
    {
        var signature = Header("X-Hub-Signature-256", true);
        var delivery = Header("X-GitHub-Delivery"); var eventName = Header("X-GitHub-Event");
        if (Request.ContentLength > GitWebhookIngress.MaximumBodyBytes) throw TooLarge();
        using var memory = new MemoryStream(); var buffer = new byte[16 * 1024];
        try
        {
            int read;
            while ((read = await Request.Body.ReadAsync(buffer, ct)) != 0)
            {
                if (memory.Length + read > GitWebhookIngress.MaximumBodyBytes) throw TooLarge();
                await memory.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413) { throw TooLarge(); }
        var receipt = await mediator.Send(new AcceptGitWebhookCommand(new(integrationId, delivery, eventName,
            signature, memory.ToArray(), HttpContext.TraceIdentifier)), ct);
        return StatusCode(receipt.Conflict ? 409 : receipt.Duplicate ? 200 : 202, receipt);
    }
    private string Header(string name, bool signature = false)
    {
        if (!Request.Headers.TryGetValue(name, out var values) || values.Count != 1 || string.IsNullOrEmpty(values[0]))
            throw new MethodOperationException(signature ? 401 : 400, "invalid_git_headers", signature ? "Assinatura do webhook ausente ou inválida." : "Cabeçalhos obrigatórios do webhook inválidos.");
        return values[0]!;
    }
    private static MethodOperationException TooLarge() => new(413, "git_payload_too_large", "Webhook acima de 5 MiB recusado sem efeito. Avise o administrador para reenvio controlado.");
}

/// <summary>Recusa assinaturas malformadas antes de ler/alocar o corpo; HMAC válido sempre alcança a inbox.</summary>
public sealed class MethodGitWebhookRateFilter(IMethodGitStore store, GitWebhookIngressLimiter limiter,
    IConfiguration configuration, TimeProvider? clock = null) : IAsyncResourceFilter
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!configuration.GetValue<bool>("MethodGit:Enabled")) { context.Result = Failure(503, "Integração Git do método desabilitada nesta instalação."); return; }
        if (!Guid.TryParse(context.RouteData.Values["integrationId"]?.ToString(), out var id)) { context.Result = Failure(400, "Integração inválida."); return; }
        try
        {
            var repository = await store.GetRepositoryForIngressAsync(id, context.HttpContext.RequestAborted);
            if (repository is null || !repository.Enabled) { context.Result = Failure(401, "Webhook não autenticado."); return; }
            var signatures = context.HttpContext.Request.Headers["X-Hub-Signature-256"];
            if (signatures.Count != 1 || !GitHubCryptography.HasSignatureFormat(signatures[0]))
            {
                var allowed = limiter.TryConsumeInvalid(id, _clock.GetUtcNow());
                if (!allowed) context.HttpContext.Response.Headers.RetryAfter = "60";
                context.Result = Failure(allowed ? 401 : 429, allowed ? "Assinatura do webhook ausente ou inválida."
                    : "Limite de assinaturas inválidas atingido. Confira o segredo e reenvie manualmente as entregas recusadas no GitHub."); return;
            }
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) { context.Result = Failure(503, "Não foi possível consultar a integração. O webhook não foi aceito; tente novamente."); return; }
        await next();
    }
    private static ObjectResult Failure(int status, string message) => new(new ProblemDetails { Status = status, Title = message }) { StatusCode = status };
}
