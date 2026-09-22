using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Marcador persistente e irreversível da configuração inicial da instalação.</summary>
public sealed class InstallationState
{
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;
    public bool IsInitialized { get; private set; }
    public DateTimeOffset? InitializedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    private InstallationState() { }

    public static InstallationState CreatePending() => new();

    public void MarkInitialized(DateTimeOffset initializedAt)
    {
        DomainException.Garantir(!IsInitialized, "A instalação já foi configurada.");
        IsInitialized = true;
        InitializedAt = initializedAt;
    }
}
