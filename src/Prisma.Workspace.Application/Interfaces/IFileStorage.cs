namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Armazenamento físico de arquivos de anexo. A Application decide O QUE
/// gravar; onde e como gravar é detalhe da implementação (Infrastructure).
/// </summary>
public interface IFileStorage
{
    /// <summary>Grava o conteúdo e devolve o caminho relativo para persistir no banco.</summary>
    Task<string> SaveAsync(string folder, string storedFileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Abre o arquivo para leitura, ou nulo se não existir/caminho inválido.</summary>
    Stream? OpenRead(string relativePath);

    /// <summary>Remove um arquivo já persistido quando uma operação composta falha.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
