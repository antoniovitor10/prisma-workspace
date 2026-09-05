using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.OpenApi;

public sealed class ValidationErrorResponse : ProblemDetails
{
    public IReadOnlyDictionary<string, string[]> Errors { get; init; }
        = new Dictionary<string, string[]>();
}

public static class ApiErrors
{
    public static ValidationErrorResponse Validation(
        IReadOnlyDictionary<string, string[]> errors,
        string? traceId = null)
        => new()
        {
            Type = "validation_error",
            Title = "Erro de validação",
            Status = StatusCodes.Status400BadRequest,
            Errors = errors,
            Extensions = { ["traceId"] = traceId }
        };

    public static ProblemDetails Problem(int status, string type, string title, string detail, string? traceId = null)
        => new()
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
            Extensions = { ["traceId"] = traceId }
        };
}
