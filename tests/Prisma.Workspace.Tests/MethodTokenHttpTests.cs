using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prisma.Workspace.Api.Authentication;
using Prisma.Workspace.Api.Controllers;
using Prisma.Workspace.Api.OpenApi;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;

namespace Prisma.Workspace.Tests;

/// <summary>Handler e controller reais em host isolado; o pipeline de produto completo será provado na F1/F2.</summary>
public sealed class MethodTokenHttpTests
{
    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    [InlineData("sistema")]
    public async Task MethodHandler_ValidatesOpaqueCredentialAndNeverCreatesInteractiveAuthority(string role)
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Token HTTP", role));
        await using var app = Host(f);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:3136") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
        client.DefaultRequestHeaders.Add("X-Request-Origin", "ui");
        client.DefaultRequestHeaders.Add("X-Method-Role", "humano");
        var identity = await client.GetFromJsonAsync<IdentityResponse>("/identity");
        Assert.NotNull(identity);
        Assert.Equal(role, identity.Role);
        Assert.Equal("methodToken", identity.AuthenticationKind);
        Assert.Equal("mcp", identity.Channel);
        Assert.False(identity.Interactive);
        Assert.Equal(role == "humano" ? MethodTokenTests.Fixture.HumanA : $"method:{issued.Metadata.PrincipalId:D}", identity.Actor);
        var path = $"/api/projects/{f.ProjectId}/method/tokens";
        using var forbidden = await client.PostAsJsonAsync(path, new IssueMethodTokenRequest("Outro token", "humano"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.DoesNotContain(issued.Token, await forbidden.Content.ReadAsStringAsync());
        using var listed = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        await f.Service().RevokeAsync(f.ProjectId, issued.Metadata.Id);
        using var revoked = await client.GetAsync("/identity");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.DoesNotContain(issued.Token, await revoked.Content.ReadAsStringAsync());
        await app.StopAsync();
    }

    [Fact]
    public async Task MissingMalformedExpiredAndQueryCredentialsAre401WithoutLeak()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Token HTTP", "agente", 1));
        await using var app = Host(f);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:3136") };
        using var missing = await client.GetAsync("/identity");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var query = await client.GetAsync($"/identity?access_token={issued.Token}");
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
        foreach (var invalid in new[] { "pmt_invalid", "eyJhbGciOiJIUzI1NiJ9.payload.signature" })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            using var response = await client.GetAsync("/identity");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(invalid, await response.Content.ReadAsStringAsync());
        }
        f.Clock.Advance(TimeSpan.FromDays(1));
        client.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
        using var expired = await client.GetAsync("/identity");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task ControllerRejectsInvalidValidityBeforeIssuance()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Token HTTP", "agente"));
        await using var app = Host(f);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:3136") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
        using var response = await client.PostAsJsonAsync($"/api/projects/{f.ProjectId}/method/tokens",
            new IssueMethodTokenRequest("Inválido", "agente", 91));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await f.Service().ListAsync(f.ProjectId));
        await app.StopAsync();
    }

    private static WebApplication Host(MethodTokenTests.Fixture f)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:3136");
        builder.Services.AddScoped(_ =>
        {
            var org = new CurrentOrganizationContext(); org.Set(f.OrgId); return org;
        });
        builder.Services.AddScoped(sp => new AppDbContext(f.Options, sp.GetRequiredService<CurrentOrganizationContext>()));
        builder.Services.AddScoped<IOrganizationContext>(sp => sp.GetRequiredService<CurrentOrganizationContext>());
        builder.Services.AddScoped<MethodAccessContext>();
        builder.Services.AddScoped<IMethodAccessContext>(sp => sp.GetRequiredService<MethodAccessContext>());
        builder.Services.AddScoped<IMethodContextInitializer>(sp => sp.GetRequiredService<MethodAccessContext>());
        builder.Services.AddSingleton<TimeProvider>(f.Clock);
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<MethodPermissionEvaluator>();
        builder.Services.AddScoped<IMethodTokenService, MethodTokenService>();
        builder.Services.AddAuthentication(MethodTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, MethodTokenAuthenticationHandler>(MethodTokenAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(MethodTokensController).Assembly);
        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            try { await next(http); }
            catch (MethodOperationException e)
            {
                http.Response.StatusCode = e.StatusCode;
                await http.Response.WriteAsJsonAsync(ApiErrors.Problem(e.StatusCode, e.Code, "Operação recusada", e.Message));
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/identity", (IMethodAccessContext context) =>
        {
            var actor = context.Current!;
            return new IdentityResponse(actor.ActorId, actor.Role.ToCode(), JsonNamingPolicy.CamelCase.ConvertName(actor.AuthenticationKind.ToString()), actor.IsInteractiveHuman, actor.Channel);
        }).RequireAuthorization();
        return app;
    }

    private sealed record IdentityResponse(string Actor, string Role, string AuthenticationKind, bool Interactive, string Channel);
}
