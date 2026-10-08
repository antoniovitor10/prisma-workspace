using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.Ai;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services;
using Prisma.Workspace.Infrastructure.Services.Ai;
using Prisma.Workspace.Infrastructure.Identity;

namespace Prisma.Workspace.Tests;

public class AiFoundationTests
{
    private sealed class OrganizationContext(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class Permissions : IPermissionService
    {
        public Task<bool> HasAsync(string userId, PlatformPermission p, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsureAsync(string userId, PlatformPermission p, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Provider : IAiProviderFactory, IAiChatProvider
    {
        public IReadOnlyList<AiProviderMessage> Received = [];
        public int Calls;
        public Guid LastConnectionId;
        public string LastModel = "";
        public bool LastNative;
        public IAiChatProvider Create(AiProviderConnection c) => this;
        public async Task<AiProviderResult> CompleteAsync(AiProviderConnection c, string? secret, IReadOnlyList<AiProviderMessage> messages, bool native, int maxTokens, Func<string, Task>? delta, CancellationToken ct)
        {
            Calls++; Received = messages; LastConnectionId = c.Id; LastModel = c.Model; LastNative = native;
            const string answer = "Resposta [T:9999] e [P:NEGADO].";
            if (delta is not null) await delta(answer);
            return new(answer, [], 10, 5);
        }
    }
    private sealed class Tools : IAiWorkspaceTools
    {
        public Task ValidateContextAsync(JsonElement c, string user, CancellationToken ct) => Task.CompletedTask;
        public Task<object> ExecuteAsync(string name, JsonElement a, string user, JsonElement c, CancellationToken ct) => Task.FromResult<object>(new { });
        public Task<IReadOnlyList<AiSource>> ResolveSourcesAsync(string text, string user, CancellationToken ct) => Task.FromResult<IReadOnlyList<AiSource>>([]);
    }
    private sealed class Clients : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
    private sealed class ChatCatalogClient : HttpMessageHandler, IHttpClientFactory
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "{\"data\":[{\"id\":\"model-a\",\"name\":\"Modelo A\"},{\"id\":\"model-b\",\"name\":\"Modelo B\"}]}";
        public Exception? Error { get; set; }
        public int Requests { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            if (Error is not null) throw Error;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class WaitingProvider : IAiProviderFactory, IAiChatProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IAiChatProvider Create(AiProviderConnection connection) => this;
        public async Task<AiProviderResult> CompleteAsync(AiProviderConnection connection, string? secret, IReadOnlyList<AiProviderMessage> messages, bool nativeTools, int maxTokens, Func<string, Task>? delta, CancellationToken ct)
        {
            Started.TrySetResult(); await Release.Task.WaitAsync(ct); return new("OK", [], 1, 1);
        }
    }
    private sealed class RefusingCliProvider : IAiProviderFactory, IAiChatProvider
    {
        public IAiChatProvider Create(AiProviderConnection connection) => this;
        public Task<AiProviderResult> CompleteAsync(AiProviderConnection connection, string? secret, IReadOnlyList<AiProviderMessage> messages, bool nativeTools, int maxTokens, Func<string, Task>? delta, CancellationToken ct)
            => throw new AiException(502, "Confira a instalação da ponte ou inicie o login novamente.");
    }
    [Theory]
    [InlineData("tenant")]
    [InlineData("platform")]
    public async Task CliInferenceErrorsKeepAdministrativeDetailsOutOfCommonUsersChat(string user)
    {
        await using var f = new Fixture(factory: new RefusingCliProvider()); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.Type = "CliSubscription"; connection.Provider = "OpenAI"; await f.Db.SaveChangesAsync();
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation", user)).GetProperty("Id").GetGuid();
        var error = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", user, id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(502, error.Status);
        Assert.Equal(user == "tenant" ? "Esta conexão está indisponível no momento. Tente novamente." : "Confira a instalação da ponte ou inicie o login novamente.", error.Message);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(503)]
    [InlineData(-1)]
    [InlineData(-2)]
    public async Task TemporarilyUnavailableCatalogRefusesSendingWithoutInferenceAndPreservesSelection(int failure)
    {
        var http = new ChatCatalogClient(); await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = new AiProviderConnection { Name = "Segunda", Model = "model-a", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true };
        f.Db.AiProviderConnections.Add(connection); await f.Db.SaveChangesAsync();
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation", user: "tenant")).GetProperty("Id").GetGuid();
        await f.Run("selectConversation", user: "tenant", id: id, input: new { connectionId = connection.Id, model = "model-b" });
        http.Status = failure > 0 ? (HttpStatusCode)failure : HttpStatusCode.OK;
        http.Error = failure == -1 ? new HttpRequestException("Rede") : failure == -2 ? new TaskCanceledException("Prazo") : null;
        await f.Run("chat.models", user: "tenant", id: connection.Id, input: new { refresh = true });
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var requests = http.Requests;
            var error = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "tenant", id, JsonSerializer.SerializeToElement(new { text = "Preserve esta pergunta" }), (_, _) => Task.CompletedTask, default));
            Assert.Equal(409, error.Status);
            Assert.Equal("Não foi possível consultar os modelos desta conexão agora. Tente novamente", error.Message);
            Assert.Equal(requests + 1, http.Requests);
            Assert.Equal(0, f.Provider.Calls); Assert.Empty(f.Db.AiMessages); Assert.Empty(f.Db.AiUsageRecords);
            Assert.Equal(connection.Id, f.Db.AiConversations.Single().SelectedConnectionId);
            Assert.Equal("model-b", f.Db.AiConversations.Single().SelectedModel);
        }
        Assert.False(JsonSerializer.SerializeToElement(await f.Run("conversation", "tenant", id)).GetProperty("selectionAvailable").GetBoolean());
        http.Status = HttpStatusCode.OK; http.Error = null;
        await f.Service.ExecuteAsync("message", "tenant", id, JsonSerializer.SerializeToElement(new { text = "Preserve esta pergunta" }), (_, _) => Task.CompletedTask, default);
        Assert.Equal(1, f.Provider.Calls); Assert.Equal(connection.Id, f.Provider.LastConnectionId); Assert.Equal("model-b", f.Provider.LastModel);
        Assert.Equal(connection.Id, f.Db.AiUsageRecords.Single().ConnectionId); Assert.Equal("model-b", f.Db.AiUsageRecords.Single().Model);
        Assert.Equal(2, f.Db.AiMessages.Count()); Assert.True(f.Db.AiProviderConnections.Single(x => x.IsActive).Id != connection.Id);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(503)]
    [InlineData(-1)]
    [InlineData(-2)]
    public async Task TransientCatalogFailuresNeverBecomeManualFallbackOrEnterCache(int failure)
    {
        var http = new ChatCatalogClient { Status = failure > 0 ? (HttpStatusCode)failure : HttpStatusCode.OK, Error = failure == -1 ? new HttpRequestException("Rede") : failure == -2 ? new TaskCanceledException("Prazo") : null };
        await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.Model = "model-a"; connection.BaseUrl = "http://catalog.invalid/v1"; await f.Db.SaveChangesAsync();
        var admin = JsonSerializer.SerializeToElement(await f.Run("admin.models", input: new { connectionId = connection.Id, provider = connection.Provider, type = connection.Type, baseUrl = connection.BaseUrl }));
        Assert.False(admin.GetProperty("manualAllowed").GetBoolean());
        var catalog = JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id));
        Assert.Equal("unavailable", catalog.GetProperty("State").GetString()); Assert.Empty(catalog.GetProperty("Models").EnumerateArray());
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation")).GetProperty("Id").GetGuid();
        var error = await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", id: id, input: new { connectionId = connection.Id, model = "model-b" }));
        Assert.Equal(409, error.Status); Assert.DoesNotContain("O modelo não está mais disponível", error.Message);
        Assert.Null(f.Db.AiConversations.Single().SelectedConnectionId);
        http.Status = HttpStatusCode.OK; http.Error = null;
        catalog = JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id));
        Assert.Equal("ready", catalog.GetProperty("State").GetString()); Assert.Equal(2, catalog.GetProperty("Models").GetArrayLength());
        await f.Run("selectConversation", id: id, input: new { connectionId = connection.Id, model = "model-b" });
        Assert.Equal("model-b", f.Db.AiConversations.Single().SelectedModel); Assert.Equal(4, http.Requests);
    }
    [Theory]
    [InlineData(404)]
    [InlineData(405)]
    public async Task EndpointWithoutCatalogOnlyAllowsItsConfiguredTestedModel(int status)
    {
        var http = new ChatCatalogClient { Status = (HttpStatusCode)status }; await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.Model = "model-a"; connection.BaseUrl = "http://catalog.invalid/v1"; await f.Db.SaveChangesAsync();
        var catalog = JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id));
        Assert.Equal("ready", catalog.GetProperty("State").GetString()); Assert.Single(catalog.GetProperty("Models").EnumerateArray());
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation")).GetProperty("Id").GetGuid();
        Assert.Equal(409, (await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", id: id, input: new { connectionId = connection.Id, model = "model-b" }))).Status);
        await f.Run("selectConversation", id: id, input: new { connectionId = connection.Id, model = "model-a" });
        Assert.Equal("model-a", f.Db.AiConversations.Single().SelectedModel);
    }
    [Fact]
    public async Task EmptyCatalogDoesNotOfferAConfiguredModelThatWasRemoved()
    {
        var http = new ChatCatalogClient { Body = "{\"data\":[]}" }; await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.BaseUrl = "http://catalog.invalid/v1"; await f.Db.SaveChangesAsync();
        var catalog = JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id));
        Assert.Equal("empty", catalog.GetProperty("State").GetString()); Assert.Empty(catalog.GetProperty("Models").EnumerateArray());
        Assert.Contains("Nenhum modelo", catalog.GetProperty("Message").GetString());
        var conversation = new AiConversation { OrganizationId = f.Org, UserId = "tenant", SelectedConnectionId = connection.Id, SelectedModel = connection.Model };
        f.Db.AiConversations.Add(conversation); await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "tenant", conversation.Id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(409, error.Status); Assert.Contains("O modelo não está mais disponível", error.Message);
        Assert.Equal(connection.Model, conversation.SelectedModel); Assert.Equal(0, f.Provider.Calls); Assert.Empty(f.Db.AiMessages);
        http.Body = "{\"data\":[{\"id\":\"model-a\"}]}";
        Assert.Equal("ready", JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id)).GetProperty("State").GetString());
        Assert.Equal(3, http.Requests);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task InvalidCatalogResponseDoesNotDeclareTheSelectedModelRemoved(string body)
    {
        var http = new ChatCatalogClient { Body = body }; await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.BaseUrl = "http://catalog.invalid/v1"; await f.Db.SaveChangesAsync();
        var conversation = new AiConversation { OrganizationId = f.Org, UserId = "tenant", SelectedConnectionId = connection.Id, SelectedModel = connection.Model };
        f.Db.AiConversations.Add(conversation); await f.Db.SaveChangesAsync();
        var catalog = JsonSerializer.SerializeToElement(await f.Run("chat.models", "tenant", connection.Id));
        Assert.Equal("unavailable", catalog.GetProperty("State").GetString());
        var error = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "tenant", conversation.Id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(409, error.Status); Assert.Equal("Não foi possível consultar os modelos desta conexão agora. Tente novamente", error.Message);
        Assert.Equal(0, f.Provider.Calls); Assert.Empty(f.Db.AiMessages);
        http.Body = "{\"data\":[{\"id\":\"fake\"}]}";
        await f.Service.ExecuteAsync("message", "tenant", conversation.Id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default);
        Assert.Equal(1, f.Provider.Calls); Assert.Equal(3, http.Requests);
    }
    [Theory]
    [InlineData("removed")]
    [InlineData("deleted")]
    [InlineData("method")]
    [InlineData("revoked")]
    public async Task SelectionAvailabilityMatchesSendingForIneligibleSelections(string change)
    {
        var http = new ChatCatalogClient(); await using var f = new Fixture(http: http); await f.SeedAsync();
        var second = new AiProviderConnection { Name = "Segunda", Model = "model-a", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true };
        f.Db.AiProviderConnections.Add(second); await f.Db.SaveChangesAsync();
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation")).GetProperty("Id").GetGuid();
        await f.Run("selectConversation", id: id, input: new { connectionId = second.Id, model = "model-b" });
        Assert.True(JsonSerializer.SerializeToElement(await f.Run("conversation", id: id)).GetProperty("selectionAvailable").GetBoolean());
        if (change == "removed") { http.Body = "{\"data\":[{\"id\":\"model-a\"}]}"; await f.Run("chat.models", id: second.Id, input: new { refresh = true }); }
        if (change == "deleted") await f.Run("admin.deleteConnection", id: second.Id);
        if (change == "method") { second.Type = "CliSubscription"; await f.Db.SaveChangesAsync(); }
        if (change == "revoked") { second.TestSucceeded = false; await f.Db.SaveChangesAsync(); }
        var detail = JsonSerializer.SerializeToElement(await f.Run("conversation", id: id));
        Assert.False(detail.GetProperty("selectionAvailable").GetBoolean());
        var list = JsonSerializer.SerializeToElement(await f.Run("conversations"));
        Assert.False(list[0].GetProperty("selectionAvailable").GetBoolean());
        var error = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "platform", id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(409, error.Status);
        if (change == "removed") Assert.Contains("O modelo não está mais disponível", error.Message);
        Assert.Equal(second.Id, f.Db.AiConversations.Single().SelectedConnectionId);
        Assert.Equal("model-b", f.Db.AiConversations.Single().SelectedModel);
        Assert.Empty(f.Db.AiMessages); Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task ListingOneHundredSelectedConversationsDoesNotFetchAnUnavailableCatalogButDetailDoes()
    {
        var http = new ChatCatalogClient { Status = HttpStatusCode.ServiceUnavailable };
        await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = new AiProviderConnection { Name = "Segunda", Model = "model-a", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true };
        f.Db.AiProviderConnections.Add(connection);
        var conversations = Enumerable.Range(0, 100).Select(_ => new AiConversation { OrganizationId = f.Org, UserId = "tenant", SelectedConnectionId = connection.Id, SelectedModel = "model-b" }).ToArray();
        f.Db.AiConversations.AddRange(conversations); await f.Db.SaveChangesAsync();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var list = JsonSerializer.SerializeToElement(await f.Run("conversations", "tenant"));
            Assert.Equal(100, list.GetArrayLength()); Assert.All(list.EnumerateArray(), row => Assert.True(row.GetProperty("selectionAvailable").GetBoolean()));
            Assert.Equal(0, http.Requests);
        }
        var detail = JsonSerializer.SerializeToElement(await f.Run("conversation", "tenant", conversations[0].Id));
        Assert.False(detail.GetProperty("selectionAvailable").GetBoolean()); Assert.Equal(1, http.Requests);
        var afterFailure = JsonSerializer.SerializeToElement(await f.Run("conversations", "tenant"));
        Assert.All(afterFailure.EnumerateArray(), row => Assert.True(row.GetProperty("selectionAvailable").GetBoolean()));
        Assert.Equal(1, http.Requests); Assert.Equal(0, f.Provider.Calls);
        Assert.Equal(connection.Id, conversations[0].SelectedConnectionId); Assert.Equal("model-b", conversations[0].SelectedModel);
    }
    [Theory]
    [InlineData("unchanged")]
    [InlineData("testedAt")]
    [InlineData("model")]
    public async Task ListingOnlyUsesCatalogAlreadyCachedForTheCurrentConnectionState(string change)
    {
        var http = new ChatCatalogClient { Body = "{\"data\":[{\"id\":\"model-a\"}]}" };
        await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = new AiProviderConnection { Name = "Segunda", Model = "model-a", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true };
        f.Db.AiProviderConnections.Add(connection);
        f.Db.AiConversations.Add(new AiConversation { OrganizationId = f.Org, UserId = "tenant", SelectedConnectionId = connection.Id, SelectedModel = "model-b" });
        await f.Db.SaveChangesAsync(); await f.Run("chat.models", "tenant", connection.Id);
        Assert.Equal(1, http.Requests); http.Status = HttpStatusCode.ServiceUnavailable;
        if (change == "testedAt") connection.TestedAt = DateTimeOffset.UtcNow;
        if (change == "model") connection.Model = "model-c";
        await f.Db.SaveChangesAsync();
        var list = JsonSerializer.SerializeToElement(await f.Run("conversations", "tenant"));
        Assert.Equal(change != "unchanged", list[0].GetProperty("selectionAvailable").GetBoolean());
        Assert.Equal(1, http.Requests); Assert.Equal(0, f.Provider.Calls);
    }
    [Theory]
    [InlineData("save")]
    [InlineData("test")]
    public async Task AdministrativeChangesInvalidateChatCatalogCache(string operation)
    {
        var http = new ChatCatalogClient(); await using var f = new Fixture(http: http); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.BaseUrl = "http://catalog.invalid/v1"; connection.Model = "model-a"; await f.Db.SaveChangesAsync();
        await f.Run("chat.models", id: connection.Id); await f.Run("chat.models", id: connection.Id);
        Assert.Equal(1, http.Requests);
        http.Body = "{\"data\":[{\"id\":\"model-c\"}]}";
        if (operation == "save")
        {
            await f.Run("admin.saveConnection", id: connection.Id, input: new { name = connection.Name, provider = connection.Provider, type = connection.Type, baseUrl = connection.BaseUrl, model = connection.Model });
            Assert.Equal(409, (await Assert.ThrowsAsync<AiException>(() => f.Run("chat.models", id: connection.Id))).Status);
        }
        await f.Run("admin.testConnection", id: connection.Id);
        if (operation == "save") await f.Run("admin.activateConnection", id: connection.Id);
        var data = JsonSerializer.SerializeToElement(await f.Run("chat.models", id: connection.Id));
        Assert.Equal(2, http.Requests); Assert.Equal("model-c", data.GetProperty("Models")[0].GetProperty("Id").GetString());
    }
    [Fact]
    public async Task CommonUserOptionsExcludeIneligibleConnectionsAndPrivateConfiguration()
    {
        await using var f = new Fixture(http: new ChatCatalogClient()); await f.SeedAsync();
        var primary = f.Db.AiProviderConnections.Single(); primary.BaseUrl = "http://private.invalid/v1"; primary.SecretSuffix = "ABCD";
        f.Db.AiProviderConnections.AddRange(new AiProviderConnection { Name = "Sem teste" }, new AiProviderConnection { Name = "Método inválido", TestSucceeded = true, Provider = "Custom", Type = "CliSubscription" }, new AiProviderConnection { Name = "Transcrição", TestSucceeded = true, Purpose = "Transcription" });
        await f.Db.SaveChangesAsync();
        var json = JsonSerializer.SerializeToElement(await f.Run("chat.options", user: "tenant"));
        Assert.Single(json.GetProperty("connections").EnumerateArray());
        Assert.Equal(primary.Id, json.GetProperty("connections")[0].GetProperty("Id").GetGuid());
        Assert.DoesNotContain("private.invalid", json.ToString()); Assert.DoesNotContain("ABCD", json.ToString());
        Assert.DoesNotContain("Secret", json.ToString()); Assert.DoesNotContain("login", json.ToString(), StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData(503)]
    [InlineData(409)]
    [InlineData(0)]
    public async Task CliCatalogFailuresAreNeutralForCommonUsersAndDetailedForAdministrators(int status)
    {
        var http = new ChatCatalogClient { Status = status == 0 ? HttpStatusCode.OK : (HttpStatusCode)status, Body = "{\"models\":[],\"state\":\"authenticationRequired\",\"message\":\"Inicie o login oficial e confira a instalação.\"}" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:BridgeToken"] = "fake-test", ["Ai:BridgeUrl"] = "http://bridge.invalid/v1" }).Build();
        await using var f = new Fixture(http: http, configuration: config); await f.SeedAsync();
        var connection = f.Db.AiProviderConnections.Single(); connection.Type = "CliSubscription"; connection.Provider = "OpenAI"; await f.Db.SaveChangesAsync();
        foreach (var user in new[] { "tenant", "platform" })
        {
            var data = JsonSerializer.SerializeToElement(await f.Run("chat.models", user, connection.Id));
            var message = data.GetProperty("Message").GetString()!;
            if (user == "tenant") { Assert.Equal("Esta conexão está indisponível no momento. Tente novamente.", message); }
            else { Assert.True(message.Contains("login", StringComparison.OrdinalIgnoreCase) || message.Contains("instalação")); }
            var id = JsonSerializer.SerializeToElement(await f.Run("createConversation", user)).GetProperty("Id").GetGuid();
            var selectionError = await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", user, id, new { connectionId = connection.Id, model = connection.Model }));
            Assert.Equal(409, selectionError.Status); Assert.Equal(message, selectionError.Message);
            var row = f.Db.AiConversations.Single(x => x.Id == id); row.SelectedConnectionId = connection.Id; row.SelectedModel = connection.Model; await f.Db.SaveChangesAsync();
            var sendError = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", user, id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default));
            if (status == 0) Assert.Equal(message, sendError.Message);
            else
            {
                Assert.Equal(409, sendError.Status);
                Assert.StartsWith("Não foi possível consultar os modelos desta conexão agora. Tente novamente", sendError.Message);
                if (user == "platform") Assert.Contains(message, sendError.Message);
                else Assert.Equal("Não foi possível consultar os modelos desta conexão agora. Tente novamente", sendError.Message);
            }
        }
        Assert.Empty(f.Db.AiMessages); Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task SelectionCannotChangeWhileTheSameConversationIsGenerating()
    {
        var provider = new WaitingProvider(); await using var f = new Fixture(factory: provider); await f.SeedAsync();
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation")).GetProperty("Id").GetGuid();
        var running = f.Service.ExecuteAsync("message", "platform", id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Assert.Equal(409, (await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", id: id, input: new { connectionId = (Guid?)null, model = (string?)null }))).Status); }
        finally { provider.Release.TrySetResult(); await running; }
    }
    [Fact]
    public async Task ConversationSelectionIsPrivatePersistentAndDoesNotModifyTheGlobalConnection()
    {
        await using var f = new Fixture(http: new ChatCatalogClient()); await f.SeedAsync();
        var primary = f.Db.AiProviderConnections.Single();
        var second = new AiProviderConnection { Name = "Segunda", Model = "model-a", Provider = "Custom", Type = "OpenAiCompatible", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true, SupportsTools = true, InputPrice = 2, OutputPrice = 3, TestedAt = DateTimeOffset.UtcNow };
        f.Db.AiProviderConnections.Add(second); await f.Db.SaveChangesAsync();
        var created = JsonSerializer.SerializeToElement(await f.Run("createConversation"));
        var id = created.GetProperty("Id").GetGuid();
        var options = JsonSerializer.Serialize(await f.Run("chat.options", user: "regular"));
        Assert.Contains("Segunda", options); Assert.DoesNotContain("catalog.invalid", options); Assert.DoesNotContain("Secret", options);
        await f.Run("selectConversation", id: id, input: new { connectionId = second.Id, model = "model-b" });
        var row = f.Db.AiConversations.Single(); Assert.Equal(second.Id, row.SelectedConnectionId); Assert.Equal("model-b", row.SelectedModel);
        Assert.True(primary.IsActive); Assert.False(second.IsActive); Assert.Equal("model-a", second.Model);
        Assert.Equal(404, (await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", user: "other", id: id, input: new { connectionId = second.Id, model = "model-a" }))).Status);
        await f.Service.ExecuteAsync("message", "platform", id, JsonSerializer.SerializeToElement(new { text = "Olá" }), (_, _) => Task.CompletedTask, default);
        Assert.Equal(second.Id, f.Provider.LastConnectionId); Assert.Equal("model-b", f.Provider.LastModel); Assert.False(f.Provider.LastNative);
        var usage = f.Db.AiUsageRecords.Single(); Assert.Equal(second.Id, usage.ConnectionId); Assert.Equal("model-b", usage.Model); Assert.Null(usage.EstimatedCost);
        Assert.Equal("model-a", second.Model);
        await f.Run("selectConversation", id: id, input: new { connectionId = (Guid?)null, model = (string?)null });
        Assert.Null(row.SelectedConnectionId); Assert.Null(row.SelectedModel);
    }
    [Fact]
    public async Task InvalidOrRevokedSelectionNeverFallsBackToAnotherProvider()
    {
        await using var f = new Fixture(http: new ChatCatalogClient()); await f.SeedAsync();
        var second = new AiProviderConnection { Name = "Segunda", Model = "model-a", BaseUrl = "http://catalog.invalid/v1", TestSucceeded = true };
        f.Db.AiProviderConnections.Add(second); await f.Db.SaveChangesAsync();
        var id = JsonSerializer.SerializeToElement(await f.Run("createConversation")).GetProperty("Id").GetGuid();
        Assert.Equal(409, (await Assert.ThrowsAsync<AiException>(() => f.Run("selectConversation", id: id, input: new { connectionId = second.Id, model = "arbitrary" }))).Status);
        await f.Run("selectConversation", id: id, input: new { connectionId = second.Id, model = "model-a" });
        second.TestSucceeded = false; await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "platform", id, JsonSerializer.SerializeToElement(new { text = "Pergunta" }), (_, _) => Task.CompletedTask, default))).Status);
        Assert.Equal(0, f.Provider.Calls); Assert.Empty(f.Db.AiMessages);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Org { get; } = Guid.NewGuid();
        public AppDbContext Db { get; }
        public DbContextOptions<AppDbContext> Options { get; }
        public Provider Provider { get; } = new();
        public AiService Service { get; }
        public Fixture(IAiProviderFactory? factory = null, IAiWorkspaceTools? tools = null, IHttpClientFactory? http = null, IConfiguration? configuration = null)
        {
            Options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var org = new OrganizationContext(Org); Db = new(Options, org);
            Service = new(Db, org, new Permissions(), factory ?? Provider, tools ?? new Tools(), new AiRedactionService(), new AiUsageMeter(Db), new EphemeralDataProtectionProvider(), configuration ?? new ConfigurationBuilder().Build(), http ?? new Clients());
        }
        public async Task SeedAsync()
        {
            var admin = new IdentityUser { Id = "platform", UserName = "platform@example.invalid" };
            Db.Users.AddRange(admin, new IdentityUser { Id = "tenant", UserName = "tenant@example.invalid" }, new IdentityUser { Id = "other", UserName = "other@example.invalid" });
            Db.Entry(admin).Property("IsPlatformAdministrator").CurrentValue = true;
            Db.Organizations.Add(new Organization { Id = Org, Name = "Prisma", Slug = "prisma", IsActive = true });
            Db.OrganizationAiSettings.Add(new() { OrganizationId = Org, Enabled = true });
            Db.AiProviderConnections.Add(new() { Name = "Teste", Model = "fake", IsActive = true, SupportsTools = true, TestSucceeded = true });
            await Db.SaveChangesAsync();
        }
        public Task<object?> Run(string operation, string user = "platform", Guid? id = null, object? input = null)
            => Service.ExecuteAsync(operation, user, id, input is null ? null : JsonSerializer.SerializeToElement(input), null, default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task OrganizationAdministratorDoesNotAcquirePlatformAuthority()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        f.Db.OrganizationMembers.Add(OrganizationMember.Create(f.Org, "tenant", OrganizationRole.Administrator)); await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Run("admin.connections", "tenant")); Assert.Equal(403, e.Status);
        Assert.NotNull(await f.Run("admin.connections"));
    }
    [Theory]
    [InlineData("admin.models")]
    [InlineData("admin.cli.status")]
    [InlineData("admin.cli.login")]
    [InlineData("admin.cli.progress")]
    [InlineData("admin.cli.complete")]
    [InlineData("admin.cli.cancel")]
    public async Task CatalogAndCliRequirePlatformAuthority(string operation)
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Run(operation, "tenant", input: new { provider = "OpenAI", type = "ApiKey" }));
        Assert.Equal(403, e.Status);
    }
    [Theory]
    [InlineData("OpenRouter")]
    [InlineData("Custom")]
    public async Task UnsupportedCliProviderCannotBeSavedOrTested(string provider)
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Run("admin.saveConnection", input: new { name = "Invalid CLI", type = "CliSubscription", provider, model = "opus" }));
        Assert.Equal(400, e.Status);
        var old = f.Db.AiProviderConnections.Single(); old.Type = "CliSubscription"; old.Provider = provider;
        await f.Db.SaveChangesAsync();
        Assert.Equal(400, (await Assert.ThrowsAsync<AiException>(() => f.Run("admin.testConnection", id: old.Id))).Status);
        Assert.Equal(0, f.Provider.Calls);
    }
    private sealed class CatalogClient(string response) : HttpMessageHandler, IHttpClientFactory
    {
        public readonly List<(string Url, string? Bearer, string? ApiKey, string? GoogleKey)> Requests = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("x-api-key", out var keys) ? keys.Single() : null,
                request.Headers.TryGetValues("x-goog-api-key", out var google) ? google.Single() : null));
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        }
    }
    [Theory]
    [InlineData("OpenAI", "{\"data\":[{\"id\":\"gpt-chat\"},{\"id\":\"text-embedding-x\"},{\"id\":\"gpt-audio\"}]}", "gpt-chat")]
    [InlineData("Anthropic", "{\"data\":[{\"id\":\"claude-example\",\"display_name\":\"Claude Example\"}]}", "claude-example")]
    [InlineData("Gemini", "{\"models\":[{\"name\":\"models/gemini-example\",\"displayName\":\"Gemini Example\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/embedding\",\"supportedGenerationMethods\":[\"embedContent\"]}]}", "gemini-example")]
    [InlineData("OpenRouter", "{\"data\":[{\"id\":\"vendor/chat\",\"name\":\"Chat\",\"architecture\":{\"output_modalities\":[\"text\"]},\"pricing\":{\"prompt\":\"0.000002\",\"completion\":\"0.000003\"}},{\"id\":\"vendor/image\",\"architecture\":{\"output_modalities\":[\"image\"]}}]}", "vendor/chat")]
    public async Task CatalogUsesProviderProtocolAndFiltersNonChatModels(string provider, string response, string expected)
    {
        var http = new CatalogClient(response); await using var f = new Fixture(http: http); await f.SeedAsync();
        var result = JsonSerializer.Serialize(await f.Run("admin.models", input: new { provider, type = "ApiKey", secret = "private-key" }));
        using var json = JsonDocument.Parse(result); var models = json.RootElement.GetProperty("models");
        Assert.Single(models.EnumerateArray()); Assert.Equal(expected, models[0].GetProperty("Id").GetString());
        Assert.DoesNotContain("private-key", result); Assert.Equal(0, f.Provider.Calls);
        if (provider == "Gemini") Assert.Equal("private-key", http.Requests[0].GoogleKey);
        else if (provider == "Anthropic") Assert.Equal("private-key", http.Requests[0].ApiKey);
        else Assert.Equal("private-key", http.Requests[0].Bearer);
        if (provider == "OpenRouter") Assert.Equal(2, models[0].GetProperty("InputPrice").GetDecimal());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OpenRouterPublicCatalogWithEmptyUrlAndNoCredentialUsesTheOfficialEndpoint(string? baseUrl)
    {
        var http = new CatalogClient("{\"data\":[{\"id\":\"vendor/chat\"}]}");
        await using var f = new Fixture(http: http); await f.SeedAsync();
        var data = JsonSerializer.SerializeToElement(await f.Run("admin.models", input: new { provider = "OpenRouter", type = "OAuth", baseUrl, secret = (string?)null }));
        Assert.Equal("ready", data.GetProperty("state").GetString());
        Assert.Equal("https://openrouter.ai/api/v1/models", http.Requests.Single().Url);
        Assert.Null(http.Requests.Single().Bearer); Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task ChangingDestinationDoesNotForwardOrPreserveSavedKey()
    {
        var http = new CatalogClient("{\"data\":[{\"id\":\"chat\"}]}"); await using var f = new Fixture(http: http); await f.SeedAsync();
        await f.Run("admin.saveConnection", input: new { name = "Credential", type = "OpenAiCompatible", provider = "Custom", baseUrl = "https://first.invalid/v1", model = "chat", secret = "private-original" });
        var row = await f.Db.AiProviderConnections.SingleAsync(x => x.Name == "Credential");
        await f.Run("admin.models", input: new { connectionId = row.Id, type = row.Type, provider = row.Provider, baseUrl = row.BaseUrl });
        Assert.Equal("private-original", http.Requests.Last().Bearer);
        await f.Run("admin.models", input: new { connectionId = row.Id, type = row.Type, provider = row.Provider, baseUrl = "https://second.invalid/v1" });
        Assert.Null(http.Requests.Last().Bearer);
        await f.Run("admin.saveConnection", id: row.Id, input: new { name = row.Name, type = row.Type, provider = row.Provider, baseUrl = "https://second.invalid/v1", model = "chat" });
        Assert.Null(row.EncryptedSecret); Assert.Null(row.SecretSuffix); Assert.False(row.TestSucceeded);
    }
    [Fact]
    public async Task CliRequiresRiskAcknowledgmentAndNeverPretendsMissingBridgeIsAuthenticated()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        Assert.Equal(400, (await Assert.ThrowsAsync<AiException>(() => f.Run("admin.cli.login", input: new { provider = "OpenAI", acceptedRisk = false }))).Status);
        var state = JsonSerializer.Serialize(await f.Run("admin.cli.status", input: new { provider = "OpenAI" }));
        Assert.Contains("\"authenticated\":false", state); Assert.Contains("\"available\":false", state);
    }
    private sealed class LoginClient : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.NotNull(request.Content!.Headers.ContentLength);
            var input = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("\"owner\":\"platform\"", input);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"sessionId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"state\":\"starting\"}", Encoding.UTF8, "application/json") };
        }
    }
    private sealed class GeminiBridgeClient : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("http://bridge.invalid/v1/chat/completions", request.RequestUri!.ToString());
            Assert.Equal("fake-private-bridge-token", request.Headers.Authorization?.Parameter);
            Assert.NotNull(request.Content!.Headers.ContentLength);
            using var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("gemini", input.RootElement.GetProperty("adapter").GetString());
            Assert.False(input.RootElement.TryGetProperty("tools", out _));
            return new(HttpStatusCode.OK) { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"OK\"}}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":1}}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
        }
    }
    [Fact]
    public async Task LegacyGeminiCliUsesConfiguredBridgeAndNeverForwardsItsTokenToSavedHost()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:BridgeUrl"] = "http://bridge.invalid/v1" }).Build();
        var provider = new AiHttpProvider(new GeminiBridgeClient(), configuration: config);
        var connection = new AiProviderConnection { Type = "CliSubscription", Provider = "Gemini", BaseUrl = "http://untrusted-old.invalid/v1", Model = "auto" };
        var result = await provider.CompleteAsync(connection, "fake-private-bridge-token", [new("user", "OK")], false, 32, null, default);
        Assert.Equal("OK", result.Text); Assert.Equal(2, result.InputTokens); Assert.Equal(1, result.OutputTokens);
    }
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Anthropic")]
    [InlineData("Gemini")]
    public async Task StartingCliLoginInvalidatesPreviouslyTestedConnectionsOfThatAdapter(string provider)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:BridgeToken"] = "fake-bridge-token-for-test", ["Ai:BridgeUrl"] = "http://bridge.invalid/v1" }).Build();
        await using var f = new Fixture(http: new LoginClient(), configuration: config); await f.SeedAsync();
        var row = f.Db.AiProviderConnections.Single(); row.Type = "CliSubscription"; row.Provider = provider; await f.Db.SaveChangesAsync();
        var result = JsonSerializer.Serialize(await f.Run("admin.cli.login", input: new { provider, acceptedRisk = true }));
        Assert.Contains("starting", result); Assert.False(row.IsActive); Assert.False(row.TestSucceeded); Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task ConnectionApiNeverReturnsItsPlaintextOrEncryptedSecret()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var result = await f.Run("admin.saveConnection", input: new { name = "Local", type = "OpenAiCompatible", provider = "Custom", baseUrl = "http://localhost:8000/v1", model = "fake", secret = "teste-credencial-secreta-ABCD" });
        var json = JsonSerializer.Serialize(result); Assert.DoesNotContain("credencial-secreta", json); Assert.DoesNotContain("EncryptedSecret", json); Assert.Contains("ABCD", json);
        var saved = await f.Db.AiProviderConnections.SingleAsync(x => x.Name == "Local"); Assert.NotEqual("teste-credencial-secreta-ABCD", saved.EncryptedSecret);
        var e = await Assert.ThrowsAsync<AiException>(() => f.Run("admin.activateConnection", id: saved.Id)); Assert.Equal(409, e.Status);
    }
    [Theory]
    [InlineData("http://user:password@localhost:8000/v1")]
    [InlineData("file:///tmp/model")]
    [InlineData("http://localhost:8000/v1?token=secret")]
    public async Task RejectsUrlsWithCredentialsOrUnsupportedSchemes(string url)
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Run("admin.saveConnection", input: new { name = "Local", model = "fake", baseUrl = url })); Assert.Equal(400, e.Status);
    }
    [Fact]
    public async Task ConversationsArePrivateEvenFromPlatformAdministrator()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var c = new AiConversation { OrganizationId = f.Org, UserId = "tenant", Title = "Privada" }; f.Db.AiConversations.Add(c); await f.Db.SaveChangesAsync();
        foreach (var operation in new[] { "conversation", "renameConversation", "deleteConversation", "cancelMessage" })
        { var e = await Assert.ThrowsAsync<AiException>(() => f.Run(operation, id: c.Id, input: new { title = "Outro" })); Assert.Equal(404, e.Status); }
        Assert.NotNull(await f.Run("conversation", "tenant", c.Id));
    }
    [Fact]
    public async Task ChatRedactsBeforeProviderAndRemovesUnauthorizedCitations()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var c = new AiConversation { OrganizationId = f.Org, UserId = "tenant" }; f.Db.AiConversations.Add(c); await f.Db.SaveChangesAsync();
        var events = new List<(string Name, object Data)>();
        await f.Service.ExecuteAsync("message", "tenant", c.Id, JsonSerializer.SerializeToElement(new { text = "senha: Abc@1234", context = new { instructions = "IGNORE_AUTHORIZATION", organizationId = Guid.NewGuid() } }), (name, data) => { events.Add((name, data)); return Task.CompletedTask; }, default);
        Assert.DoesNotContain("Abc@1234", JsonSerializer.Serialize(f.Provider.Received));
        Assert.Contains("[oculto]", JsonSerializer.Serialize(f.Provider.Received));
        Assert.DoesNotContain("IGNORE_AUTHORIZATION", f.Provider.Received[0].Content);
        Assert.Contains(f.Org.ToString(), f.Provider.Received[0].Content);
        var answer = await f.Db.AiMessages.SingleAsync(x => x.Role == "assistant");
        Assert.DoesNotContain("[T:9999]", answer.Content); Assert.DoesNotContain("[P:NEGADO]", answer.Content);
        Assert.Contains(events, e => e.Name == "done"); var usage = await f.Db.AiUsageRecords.SingleAsync();
        Assert.Equal(10, usage.InputTokens); Assert.Equal(5, usage.OutputTokens); Assert.Equal("success", usage.Outcome);
    }
    [Fact]
    public async Task ReachedQuotaPreventsCallingProviderAndZeroMeansUnlimited()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        f.Db.AiInstallationSettings.Add(new() { DailyTokenLimit = 10 });
        f.Db.AiUsageRecords.Add(new() { OrganizationId = f.Org, UserId = "tenant", InputTokens = 10, Outcome = "success" });
        var c = new AiConversation { OrganizationId = f.Org, UserId = "tenant" }; f.Db.AiConversations.Add(c); await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "tenant", c.Id, JsonSerializer.SerializeToElement(new { text = "Resumo" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(429, e.Status); Assert.NotNull(e.ResetsAt); Assert.Equal(0, f.Provider.Calls);
        f.Db.AiInstallationSettings.Single().DailyTokenLimit = 0; await f.Db.SaveChangesAsync();
        await f.Service.ExecuteAsync("message", "tenant", c.Id, JsonSerializer.SerializeToElement(new { text = "Resumo" }), (_, _) => Task.CompletedTask, default);
        Assert.Equal(1, f.Provider.Calls);
    }
    [Fact]
    public async Task TenantFilterProtectsConversationsAndTheirMessages()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await using (var seed = new AppDbContext(options))
        {
            var c = new AiConversation { OrganizationId = second, UserId = "same-user" };
            c.Messages.Add(new AiMessage { Content = "Outro tenant" }); seed.AiConversations.Add(c); await seed.SaveChangesAsync();
        }
        await using var db = new AppDbContext(options, new OrganizationContext(first));
        Assert.Empty(await db.AiConversations.ToListAsync()); Assert.Empty(await db.AiMessages.ToListAsync());
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CostRequiresPricesForAllConsumedTokens(bool completePrices)
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var connection = await f.Db.AiProviderConnections.SingleAsync();
        connection.InputPrice = 2; connection.OutputPrice = completePrices ? 4 : null;
        await f.Db.SaveChangesAsync();
        var meter = new AiUsageMeter(f.Db);
        var record = await meter.BeginAsync(connection, "tenant", f.Org, 20, default);
        await meter.FinishAsync(record, connection, new("OK", [], 10, 5), 10, "success", default);
        Assert.Equal(completePrices ? 0.00004m : (decimal?)null, record.EstimatedCost);
        var summary = JsonSerializer.SerializeToElement(await f.Run("org.usage", "tenant"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(completePrices ? JsonValueKind.Number : JsonValueKind.Null, summary[0].GetProperty("estimatedCost").ValueKind);
    }
    [Theory]
    [InlineData("{\"key\":\"errada\",\"actions\":[]}")]
    [InlineData("{\"actions\":[{\"name\":\"get_my_work\",\"arguments\":{}}]}")]
    [InlineData("prefixo {\"key\":\"correta\",\"actions\":[]} sufixo")]
    public void RejectsInvalidFallbackEnvelopes(string text) => Assert.Throws<AiException>(() => AiService.ParseEnvelope(text, "correta"));
    [Fact]
    public void RejectsMoreThanEightActions()
    {
        var text = JsonSerializer.Serialize(new { key = "correta", actions = Enumerable.Range(0, 9).Select(_ => new { name = "get_my_work", arguments = new { } }) });
        Assert.Throws<AiException>(() => AiService.ParseEnvelope(text, "correta"));
    }
    [Fact]
    public void QuotasResetAtLocalMidnight()
    {
        var day = AiUsageMeter.Day("America/Sao_Paulo", DateTimeOffset.Parse("2026-10-06T02:59:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T03:00:00Z"), day.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T03:00:00Z"), day.End);
    }
    [Fact]
    public async Task DeactivationPurgesOnlyMembersPrivateConversations()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var member = OrganizationMember.Create(f.Org, "tenant", OrganizationRole.TeamMember); f.Db.OrganizationMembers.Add(member);
        var own = new AiConversation { OrganizationId = f.Org, UserId = "tenant" }; own.Messages.Add(new() { Content = "Privado" });
        var other = new AiConversation { OrganizationId = f.Org, UserId = "other" }; other.Messages.Add(new() { Content = "Outro" });
        f.Db.AiConversations.AddRange(own, other); await f.Db.SaveChangesAsync();
        member.Configure(OrganizationRole.TeamMember, false); await f.Db.SaveChangesAsync();
        Assert.Equal(other.Id, (await f.Db.AiConversations.SingleAsync()).Id); Assert.Equal("Outro", (await f.Db.AiMessages.SingleAsync()).Content);
    }
    private sealed class RepeatingProvider : IAiProviderFactory, IAiChatProvider
    {
        public int Calls;
        public IAiChatProvider Create(AiProviderConnection c) => this;
        public Task<AiProviderResult> CompleteAsync(AiProviderConnection c, string? s, IReadOnlyList<AiProviderMessage> m, bool native, int max, Func<string, Task>? delta, CancellationToken ct)
        { Calls++; return Task.FromResult(new AiProviderResult("", [new("call" + Calls, "get_my_work", "{}")], 1, 1)); }
    }
    [Fact]
    public async Task StopsAfterFourToolRounds()
    {
        var provider = new RepeatingProvider(); await using var f = new Fixture(provider); await f.SeedAsync();
        var c = new AiConversation { OrganizationId = f.Org, UserId = "tenant" }; f.Db.AiConversations.Add(c); await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AiException>(() => f.Service.ExecuteAsync("message", "tenant", c.Id, JsonSerializer.SerializeToElement(new { text = "Consulta" }), (_, _) => Task.CompletedTask, default));
        Assert.Equal(502, e.Status); Assert.Equal(4, provider.Calls); Assert.Equal(4, await f.Db.AiUsageRecords.CountAsync());
    }
    [Theory]
    [InlineData(PermissionScope.Project, false, false)]
    [InlineData(PermissionScope.WorkItem, false, false)]
    [InlineData(PermissionScope.WorkItem, true, false)]
    [InlineData(PermissionScope.WorkItem, false, true)]
    public async Task ToolsAndCitationsRespectVisibility(PermissionScope scope, bool archived, bool otherOrganization)
    {
        await using var f = new Fixture(); await f.SeedAsync();
        f.Db.OrganizationMembers.Add(OrganizationMember.Create(f.Org, "tenant", OrganizationRole.Administrator));
        var project = Project.Criar("TEST", "Projeto", "tenant", WorkNature.Project, WorkType.Development); project.OrganizationId = f.Org;
        if (otherOrganization) project.OrganizationId = Guid.NewGuid();
        var board = new Board { Id = Guid.NewGuid(), OrganizationId = project.OrganizationId, ProjectId = project.Id, Name = "Quadro", OwnerId = "tenant" };
        var item = new WorkItem { Id = Guid.NewGuid(), Number = 5001, BoardId = board.Id, Title = "Negada", ResponsibleId = "tenant", IsArchived = archived };
        if (otherOrganization)
        {
            await using var otherDb = new AppDbContext(f.Options, new OrganizationContext(project.OrganizationId));
            otherDb.Projects.Add(project); otherDb.Boards.Add(board); otherDb.WorkItems.Add(item);
            await otherDb.SaveChangesAsync();
        }
        else { f.Db.Projects.Add(project); f.Db.Boards.Add(board); f.Db.WorkItems.Add(item); }
        if (!archived && !otherOrganization)
            f.Db.PermissionGrants.Add(new() { Id = Guid.NewGuid(), OrganizationId = f.Org, UserId = "tenant", Permission = PlatformPermission.View, Scope = scope, ScopeId = scope == PermissionScope.Project ? project.Id : item.Id, IsAllowed = false });
        await f.Db.SaveChangesAsync();
        var organization = new OrganizationContext(f.Org); var permissions = new PermissionService(f.Db, organization); var projects = new ProjectAccessService(f.Db, permissions);
        var tools = new AiWorkspaceTools(new AiWorkspaceReadRepository(f.Db), projects, new WorkItemAccessService(f.Db, projects, permissions), permissions, new Prisma.Workspace.Infrastructure.Repositories.GlobalSearchRepository(f.Db, organization));
        var result = await tools.ExecuteAsync("get_work_item", JsonSerializer.SerializeToElement(new { id = item.Id }), "tenant", JsonSerializer.SerializeToElement(new { }), default);
        Assert.Equal("{\"unavailable\":true}", JsonSerializer.Serialize(result));
        Assert.Empty(await tools.ResolveSourcesAsync("[T:5001]", "tenant", default));
        var list = await tools.ExecuteAsync("get_my_work", JsonSerializer.SerializeToElement(new { }), "tenant", JsonSerializer.SerializeToElement(new { }), default);
        Assert.Equal("[]", JsonSerializer.Serialize(list));
    }
    private sealed class HttpHandler : HttpMessageHandler, IHttpClientFactory
    {
        public string Body = "";
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"Ol\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"á\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":2}}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
        }
    }
    [Fact]
    public async Task CompatibleAdapterConsumesStreamingAndMeasuresUsage()
    {
        var handler = new HttpHandler(); var chunks = new List<string>();
        var result = await new AiHttpProvider(handler).CompleteAsync(new() { BaseUrl = "http://localhost/v1", Model = "fake" }, null,
            [new("user", "Olá")], true, 100, s => { chunks.Add(s); return Task.CompletedTask; }, default);
        Assert.Equal("Olá", result.Text); Assert.Equal(7, result.InputTokens); Assert.Equal(2, result.OutputTokens); Assert.Equal(2, chunks.Count);
        Assert.Contains("tools", handler.Body); Assert.Contains("include_usage", handler.Body);
    }
}
