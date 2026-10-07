using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Ai;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Services.Ai;

public sealed partial class AiService
{
    private sealed record ChatCatalog(ModelOption[] Models, string State, string Message);
    private sealed record CachedChatCatalog(DateTimeOffset Expires, DateTimeOffset? TestedAt, string Model, ChatCatalog Catalog);
    private static readonly ConcurrentDictionary<Guid, CachedChatCatalog> ChatCatalogs = new();

    private static bool Eligible(AiProviderConnection connection)
    {
        if (!connection.TestSucceeded || connection.Purpose != "Chat") return false;
        try { ValidateConnectionMethod(connection.Type, connection.Provider); return true; }
        catch (AiException) { return false; }
    }
    private async Task<AiProviderConnection> EligibleConnectionAsync(Guid? id, CancellationToken ct)
    {
        var connection = await db.AiProviderConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (connection is null || !Eligible(connection)) throw new AiException(409, "Esta conexão não está mais disponível. Escolha uma conexão testada ou o padrão da instalação.");
        return connection;
    }
    private async Task<object> ChatOptionsAsync(CancellationToken ct)
    {
        var connections = await db.AiProviderConnections.AsNoTracking().Where(x => x.TestSucceeded && x.Purpose == "Chat").OrderBy(x => x.Provider).ThenBy(x => x.Name).ToListAsync(ct);
        return new { connections = connections.Where(Eligible).Select(x => new { x.Id, x.Name, x.Provider, x.Type, model = x.Model, isDefault = x.IsActive }) };
    }
    private async Task<ChatCatalog> ChatModelsAsync(AiProviderConnection connection, bool refresh, CancellationToken ct)
    {
        if (refresh) ChatCatalogs.TryRemove(connection.Id, out _);
        if (ChatCatalogs.TryGetValue(connection.Id, out var cached) && cached.Expires > DateTimeOffset.UtcNow && cached.TestedAt == connection.TestedAt && cached.Model == connection.Model) return cached.Catalog;
        var args = JsonSerializer.SerializeToElement(new { connectionId = connection.Id, provider = connection.Provider, type = connection.Type, baseUrl = connection.BaseUrl }, Json);
        var data = JsonSerializer.SerializeToElement(await ModelsAsync(args, ct), Json);
        var state = S(data, "state") ?? "unavailable";
        var models = data.TryGetProperty("models", out var array) ? array.Deserialize<ModelOption[]>(Json) ?? [] : [];
        // Em endpoint sem catálogo, só o modelo configurado/testado é elegível.
        if (models.Length == 0 && connection.Type != "CliSubscription" && B(data, "manualAllowed"))
        {
            models = [new(connection.Model, connection.Model, connection.InputPrice, connection.OutputPrice)]; state = "ready";
        }
        var catalog = new ChatCatalog(models, state, state == "ready" ? "Escolha o modelo desta conexão. O provedor confirma o acesso ao responder." : state == "authenticationRequired" ? "Esta conexão precisa de login. Peça ao Administrador da instalação para autenticar." : "Não foi possível carregar os modelos. Tente atualizar ou escolha outra conexão.");
        if (catalog.State == "ready")
        {
            if (ChatCatalogs.Count >= 256) foreach (var key in ChatCatalogs.Keys) ChatCatalogs.TryRemove(key, out _);
            ChatCatalogs[connection.Id] = new(DateTimeOffset.UtcNow.AddSeconds(60), connection.TestedAt, connection.Model, catalog);
        }
        return catalog;
    }
    private async Task<ModelOption> ValidateModelAsync(AiProviderConnection connection, string? model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > 200 || model.Any(char.IsControl)) throw new AiException(400, "Selecione um modelo válido.");
        var catalog = await ChatModelsAsync(connection, false, ct);
        if (catalog.State != "ready") throw new AiException(409, catalog.Message);
        return catalog.Models.SingleOrDefault(x => x.Id == model) ?? throw new AiException(409, "O modelo não está mais disponível nesta conexão. Atualize os modelos e escolha novamente.");
    }
    private async Task<object> SelectConversationAsync(AiConversation conversation, JsonElement input, CancellationToken ct)
    {
        using var selection = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!Running.TryAdd(conversation.Id, selection)) throw new AiException(409, "Aguarde ou interrompa a resposta antes de trocar o assistente.");
        try
        {
            var rawId = S(input, "connectionId"); var model = S(input, "model");
            if ((rawId is null or "") && (model is null or "")) { conversation.SelectedConnectionId = null; conversation.SelectedModel = null; }
            else
            {
                if (!Guid.TryParse(rawId, out var connectionId)) throw new AiException(400, "Selecione uma conexão válida.");
                var connection = await EligibleConnectionAsync(connectionId, selection.Token);
                await ValidateModelAsync(connection, model, selection.Token);
                var current = await EligibleConnectionAsync(connectionId, selection.Token);
                if (current.TestedAt != connection.TestedAt) throw new AiException(409, "A conexão mudou. Atualize os modelos e confirme sua escolha.");
                conversation.SelectedConnectionId = connectionId; conversation.SelectedModel = model;
            }
            await db.SaveChangesAsync(selection.Token);
            return new { conversation.SelectedConnectionId, conversation.SelectedModel, selectionAvailable = true };
        }
        finally { Running.TryRemove(conversation.Id, out _); }
    }
    private async Task<AiProviderConnection> ResolveConversationConnectionAsync(AiConversation conversation, CancellationToken ct)
    {
        if (conversation.SelectedConnectionId is null) return await db.AiProviderConnections.SingleAsync(x => x.IsActive, ct);
        var connection = await EligibleConnectionAsync(conversation.SelectedConnectionId, ct);
        var model = await ValidateModelAsync(connection, conversation.SelectedModel, ct);
        var current = await EligibleConnectionAsync(connection.Id, ct);
        if (current.TestedAt != connection.TestedAt) throw new AiException(409, "A conexão mudou. Atualize os modelos e confirme sua escolha.");
        var sameModel = model.Id == connection.Model;
        return new() { Id = connection.Id, Name = connection.Name, Type = connection.Type, Provider = connection.Provider,
            BaseUrl = connection.BaseUrl, EncryptedSecret = connection.EncryptedSecret, Model = model.Id, Purpose = connection.Purpose,
            IsActive = connection.IsActive, TestSucceeded = connection.TestSucceeded, TestedAt = connection.TestedAt,
            SupportsTools = sameModel && connection.SupportsTools,
            InputPrice = sameModel ? connection.InputPrice : model.InputPrice,
            OutputPrice = sameModel ? connection.OutputPrice : model.OutputPrice };
    }
}
