namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Link profundo do card no frontend (<c>{FrontendBaseUrl}/projects/{projectId}/backlog?item={workItemId}</c>).</summary>
public interface IMethodCardLinks
{
    string CardUrl(Guid projectId, Guid workItemId);
}
