namespace Detran.Kanban.Application.Common.Exceptions;

/// <summary>Usuário autenticado sem a permissão necessária no escopo solicitado.</summary>
public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string message = "Você não tem permissão para realizar esta ação.")
        : base(message)
    {
    }
}
