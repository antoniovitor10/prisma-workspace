using Prisma.Workspace.Api.Middleware;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Prisma.Workspace.Api.OpenApi;

public class OrganizationHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? string.Empty).Split('?')[0];
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/organizations", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/organizations/invitations/accept", StringComparison.OrdinalIgnoreCase))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = OrganizationContextMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Organização ativa da requisição.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
