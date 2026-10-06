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
        public IAiChatProvider Create(AiProviderConnection c) => this;
        public async Task<AiProviderResult> CompleteAsync(AiProviderConnection c, string? secret, IReadOnlyList<AiProviderMessage> messages, bool native, int maxTokens, Func<string, Task>? delta, CancellationToken ct)
        {
            Calls++; Received = messages;
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
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Org { get; } = Guid.NewGuid();
        public AppDbContext Db { get; }
        public DbContextOptions<AppDbContext> Options { get; }
        public Provider Provider { get; } = new();
        public AiService Service { get; }
        public Fixture(IAiProviderFactory? factory = null, IAiWorkspaceTools? tools = null)
        {
            Options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var org = new OrganizationContext(Org); Db = new(Options, org);
            Service = new(Db, org, new Permissions(), factory ?? Provider, tools ?? new Tools(), new AiRedactionService(), new AiUsageMeter(Db), new EphemeralDataProtectionProvider(), new ConfigurationBuilder().Build(), new Clients());
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
