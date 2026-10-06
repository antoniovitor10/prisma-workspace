namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Mascaramento obrigatório do texto enviado aos provedores de IA.</summary>
public interface IAiRedactionService
{
    string Redact(string? text);
}
