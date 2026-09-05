using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Detran.Kanban.Api.OpenApi;

public sealed class StandardResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var anonymous = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true;
        if (!anonymous)
        {
            Add(operation, "401", "Autenticação ausente ou expirada", context, typeof(ProblemDetails));
            Add(operation, "403", "Usuário sem permissão", context, typeof(ProblemDetails));
        }
        Add(operation, "400", "Requisição ou regra de negócio inválida", context, typeof(ValidationErrorResponse));
        Add(operation, "429", "Limite de requisições excedido", context, typeof(ProblemDetails));
        if (context.ApiDescription.ParameterDescriptions.Any(x => x.Type == typeof(Guid)))
            Add(operation, "404", "Recurso não encontrado", context, typeof(ProblemDetails));
    }

    private static void Add(OpenApiOperation operation, string code, string description,
        OperationFilterContext context, Type type)
    {
        if (operation.Responses.ContainsKey(code)) return;
        operation.Responses[code] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository)
                }
            }
        };
    }
}
