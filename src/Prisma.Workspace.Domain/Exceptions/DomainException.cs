using System.Diagnostics.CodeAnalysis;

namespace Prisma.Workspace.Domain.Exceptions;

/// <summary>
/// Violação de uma regra de negócio do domínio. A camada de API converte
/// esta exceção em HTTP 400 com a mensagem no corpo.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>Lança <see cref="DomainException"/> se a condição for verdadeira.</summary>
    public static void Garantir([DoesNotReturnIf(false)] bool condicaoValida, string mensagemSeInvalida)
    {
        if (!condicaoValida) throw new DomainException(mensagemSeInvalida);
    }
}
