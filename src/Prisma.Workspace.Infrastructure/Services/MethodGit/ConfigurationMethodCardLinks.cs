using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>Link profundo do card no mesmo formato do frontend (MethodSettingsPanel, ProjectWiki), a partir de FrontendBaseUrl.</summary>
public sealed class ConfigurationMethodCardLinks(IConfiguration configuration) : IMethodCardLinks
{
    public string CardUrl(Guid projectId, Guid workItemId)
    {
        var baseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/projects/{projectId:D}/backlog?item={workItemId:D}";
    }
}
