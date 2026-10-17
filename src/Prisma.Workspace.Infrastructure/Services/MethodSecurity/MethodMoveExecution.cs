using Microsoft.EntityFrameworkCore.ChangeTracking;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Services.MethodSecurity;

// Só código interno da Infrastructure estabelece o fato; nenhum DTO HTTP expõe este contexto.
internal sealed class MethodMoveExecution(MethodMoveRequest request)
{
    public MethodMoveRequest Request { get; } = request;
    public MethodMoveResult? Result { get; set; }
}

internal sealed record MethodBoardScope(Guid Id, Guid ProjectId, Guid OrganizationId);
internal sealed record MethodStoredItem(Guid BoardId, Guid? StageId, Guid? ParentId, bool IsArchived);
internal sealed record MethodItemChange(EntityEntry<WorkItem> Entry, MethodBoardScope Source,
    MethodBoardScope Destination, MethodProjectSettings Settings, MethodStoredItem? Stored,
    bool IsCreation, bool WritesStage, bool ChangesIdentity, string? MethodId, string Reason);
