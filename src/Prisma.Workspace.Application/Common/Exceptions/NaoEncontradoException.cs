namespace Prisma.Workspace.Application.Common.Exceptions;

/// <summary>
/// Recurso não encontrado (ou não pertence ao usuário logado).
/// A camada de API converte esta exceção em HTTP 404.
/// </summary>
public class NaoEncontradoException : Exception
{
    public NaoEncontradoException(string recurso) : base($"{recurso} não encontrado(a).")
    {
    }
}
