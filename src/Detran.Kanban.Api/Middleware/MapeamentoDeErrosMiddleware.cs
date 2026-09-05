using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Domain.Exceptions;
using FluentValidation;
using Detran.Kanban.Api.OpenApi;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Api.Middleware;

/// <summary>
/// Converte exceções das camadas internas em respostas HTTP:
/// DomainException e ValidationException viram 400; NaoEncontradoException vira 404.
/// Com isso os controllers ficam sem try/catch e sem regra de negócio.
/// </summary>
public class MapeamentoDeErrosMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MapeamentoDeErrosMiddleware> _logger;

    public MapeamentoDeErrosMiddleware(
        RequestDelegate next,
        ILogger<MapeamentoDeErrosMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, ApiErrors.Problem(400, "business_rule_error",
                "Regra de negócio inválida", ex.Message, context.TraceIdentifier));
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(x => ToCamelCase(string.IsNullOrWhiteSpace(x.PropertyName) ? "request" : x.PropertyName))
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).Distinct().ToArray());
            await WriteProblemAsync(context, ApiErrors.Validation(errors, context.TraceIdentifier));
        }
        catch (NaoEncontradoException ex)
        {
            await WriteProblemAsync(context, ApiErrors.Problem(404, "not_found",
                "Recurso não encontrado", ex.Message, context.TraceIdentifier));
        }
        catch (AcessoNegadoException ex)
        {
            await WriteProblemAsync(context, ApiErrors.Problem(403, "forbidden",
                "Acesso negado", ex.Message, context.TraceIdentifier));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Conflito de concorrência na requisição {TraceId}.", context.TraceIdentifier);
            await WriteProblemAsync(context, ApiErrors.Problem(409, "concurrency_conflict",
                "Conflito de edição", "O registro foi alterado por outro usuário. Atualize e tente novamente.",
                context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado na requisição {TraceId}.", context.TraceIdentifier);
            await WriteProblemAsync(context, ApiErrors.Problem(500, "internal_error",
                "Erro interno", "Não foi possível concluir a operação.", context.TraceIdentifier));
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        if (context.Response.HasStarted) throw new InvalidOperationException("Resposta já iniciada.");
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static string ToCamelCase(string value)
        => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
