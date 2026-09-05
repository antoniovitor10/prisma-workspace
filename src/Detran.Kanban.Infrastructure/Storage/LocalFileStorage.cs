using Detran.Kanban.Application.Interfaces;

namespace Detran.Kanban.Infrastructure.Storage;

/// <summary>
/// Armazena anexos no disco local, sob uma raiz fixa. Protege contra
/// path traversal: só abre caminhos que resolvem para dentro da raiz.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public async Task<string> SaveAsync(
        string folder,
        string storedFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, storedFileName);
        await using var stream = File.Create(destination);
        await content.CopyToAsync(stream, cancellationToken);

        return Path.Combine(folder, storedFileName);
    }

    public Stream? OpenRead(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
