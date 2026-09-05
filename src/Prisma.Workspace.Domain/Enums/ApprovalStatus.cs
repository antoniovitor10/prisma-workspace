namespace Prisma.Workspace.Domain.Enums;

/// <summary>
/// Situação de uma solicitação de aprovação. Persistido como int.
/// </summary>
public enum ApprovalStatus
{
    Pendente = 0,
    Aprovada = 1,
    Rejeitada = 2
}
