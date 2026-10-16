using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Prisma.Workspace.Api.Authentication;
using Prisma.Workspace.Api.Middleware;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Api.Controllers;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

// Host focado no3137 próprio; endpoints de observação exercitam o pipeline real, sem simular a autenticação.
public sealed class MethodSecurityHttpTests
{
    [Theory]
    [InlineData("agente")]
    [InlineData("humano")]
    public async Task JwtUiAndOpaqueTokenUseDifferentTrustedContextsAndProjectReads(string role)
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        await f.AddProjectAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor HTTP", role));
        var key = RandomNumberGenerator.GetBytes(32);
        await using var app = Host(f, key); await app.StartAsync();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        client.DefaultRequestHeaders.Add("X-Method-Role", "humano");
        client.DefaultRequestHeaders.Add("X-Request-Origin", "ui");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "forged-correlation");
        var method = await client.GetFromJsonAsync<Identity>("/api/method/test-identity");
        Assert.NotNull(method); Assert.Equal("MethodToken", method.AuthenticationKind); Assert.Equal(role, method.Role);
        Assert.False(method.Interactive); Assert.Equal("mcp", method.Channel); Assert.Equal("mcp", method.AuditOrigin);
        Assert.NotEqual("forged-correlation", method.CorrelationId);
        var methodProjects = await client.GetFromJsonAsync<Guid[]>("/api/workitems");
        Assert.NotNull(methodProjects); Assert.Equal([f.ProjectId], methodProjects);
        using var organization = await client.GetAsync("/api/organizations");
        Assert.Equal(HttpStatusCode.Forbidden, organization.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HumanJwt(key));
        client.DefaultRequestHeaders.Add(OrganizationContextMiddleware.HeaderName, f.OrgId.ToString("D"));
        var human = await client.GetFromJsonAsync<Identity>("/api/method/test-identity");
        Assert.NotNull(human); Assert.Equal("UiSession", human.AuthenticationKind); Assert.True(human.Interactive);
        Assert.Equal("ui", human.Channel);
        Assert.Equal(2, (await client.GetFromJsonAsync<Guid[]>("/api/workitems"))!.Length);
    }

    [Fact]
    public async Task MethodCredentialCannotMintSessionEvenWhenMalformedRevokedOrHumanCookieIsPresent()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Humano pessoal", "humano"));
        await f.Service().RevokeAsync(f.ProjectId, issued.Metadata.Id);
        var key = RandomNumberGenerator.GetBytes(32);
        await using var app = Host(f, key); await app.StartAsync();
        using var client = Client();
        client.DefaultRequestHeaders.Add("Cookie", "prisma_refresh=human-cookie-test");
        foreach (var value in new[] { issued.Token, "pmt_malformed", "PMT_malformed" })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
            using var response = await client.PostAsJsonAsync("/api/auth/refresh", new { });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("session-handler-reached", await response.Content.ReadAsStringAsync());
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var human = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, human.StatusCode);
    }

    [Fact]
    public async Task WrongTenantAndQueryCredentialAreRejectedAndRevocationAffectsTheNextHttpRequest()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        await using var app = Host(f, RandomNumberGenerator.GetBytes(32)); await app.StartAsync();
        using var client = Client();
        using var query = await client.GetAsync($"/api/workitems?access_token={Uri.EscapeDataString(issued.Token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        client.DefaultRequestHeaders.Add(OrganizationContextMiddleware.HeaderName, Guid.NewGuid().ToString("D"));
        using var wrong = await client.GetAsync("/api/workitems");
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        client.DefaultRequestHeaders.Remove(OrganizationContextMiddleware.HeaderName);
        using var valid = await client.GetAsync("/api/workitems"); Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        await f.Service().RevokeAsync(f.ProjectId, issued.Metadata.Id);
        using var revoked = await client.GetAsync("/api/workitems"); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task DisabledFeatureKeepsHumanJwtAndDoesNotAuthenticateOpaqueCredentials()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor", "agente"));
        var key = RandomNumberGenerator.GetBytes(32);
        await using var app = Host(f, key, false); await app.StartAsync();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        using var method = await client.GetAsync("/api/workitems"); Assert.Equal(HttpStatusCode.Unauthorized, method.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HumanJwt(key));
        client.DefaultRequestHeaders.Add(OrganizationContextMiddleware.HeaderName, f.OrgId.ToString("D"));
        using var normal = await client.GetAsync("/api/workitems"); Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
        using var unavailable = await client.GetAsync("/api/method/test-identity"); Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
    }

    private static WebApplication Host(MethodTokenTests.Fixture f, byte[] key, bool enabled = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:3137");
        builder.Configuration["MethodGit:Enabled"] = enabled.ToString();
        builder.Services.AddScoped<CurrentOrganizationContext>();
        builder.Services.AddScoped<IOrganizationContext>(sp => sp.GetRequiredService<CurrentOrganizationContext>());
        builder.Services.AddScoped<CurrentAuditContext>();
        builder.Services.AddScoped<IAuditContext>(sp => sp.GetRequiredService<CurrentAuditContext>());
        builder.Services.AddScoped<MethodAccessContext>();
        builder.Services.AddScoped<IMethodAccessContext>(sp => sp.GetRequiredService<MethodAccessContext>());
        builder.Services.AddScoped<IMethodContextInitializer>(sp => sp.GetRequiredService<MethodAccessContext>());
        builder.Services.AddScoped(sp => new AppDbContext(f.Options, sp.GetRequiredService<CurrentOrganizationContext>(),
            sp.GetRequiredService<CurrentAuditContext>(), sp.GetRequiredService<IMethodAccessContext>()));
        builder.Services.AddSingleton<TimeProvider>(f.Clock);
        builder.Services.AddScoped<MethodPermissionEvaluator>();
        builder.Services.AddScoped<IMethodPermissionEvaluator>(sp => sp.GetRequiredService<MethodPermissionEvaluator>());
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<IMethodTokenService, MethodTokenService>();
        builder.Services.AddScoped<IUserDirectory, UserDirectory>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = MethodAuthentication.SchemeName;
            options.DefaultChallengeScheme = MethodAuthentication.SchemeName;
        }).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = "method-http-test", ValidAudience = "method-http-test", IssuerSigningKey = new SymmetricSecurityKey(key)
        }).AddMethodBearer(enabled);
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IMethodGitStore, EfMethodGitStore>();
        builder.Services.AddScoped<IMethodProjectProvisioner, MethodProjectProvisioner>();
        builder.Services.AddScoped<IMethodProjectOperations, MethodProjectOperations>();
        builder.Services.AddControllers().AddApplicationPart(typeof(MethodProjectsController).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new ProjectControllersOnly()));
        var app = builder.Build();
        app.UseMiddleware<MethodBoundaryMiddleware>(); app.UseAuthentication();
        app.UseMiddleware<AuditContextMiddleware>(); app.UseMiddleware<OrganizationContextMiddleware>();
        app.UseMiddleware<MethodIdentityMiddleware>(); app.UseAuthorization();
        app.MapGet("/api/method/test-identity", (IMethodAccessContext access, CurrentAuditContext audit) =>
        {
            var actor = access.Current!;
            return new Identity(actor.AuthenticationKind.ToString(), actor.Role.ToCode(), actor.IsInteractiveHuman,
                actor.Channel, audit.Origin, actor.CorrelationId);
        }).RequireAuthorization();
        app.MapGet("/api/workitems", async (AppDbContext db) => await db.Projects.Select(x => x.Id).ToArrayAsync()).RequireAuthorization();
        app.MapGet("/api/organizations", () => Results.Ok()).RequireAuthorization();
        app.MapControllers();
        app.MapGet("/api/public/method-test-context", (IMethodAccessContext access) =>
            Results.Ok(access.Current?.IsInteractiveHuman == true)).AllowAnonymous();
        app.MapPost("/api/auth/refresh", () => Results.Ok("session-handler-reached")).AllowAnonymous();
        return app;
    }

    [Fact]
    public async Task AuthenticatedPublicPortalRequestDoesNotBecomeInteractiveProjectAuthorization()
    {
        await using var f = await MethodTokenTests.Fixture.CreateAsync();
        var key = RandomNumberGenerator.GetBytes(32);
        await using var app = Host(f, key); await app.StartAsync();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HumanJwt(key));
        client.DefaultRequestHeaders.Add(OrganizationContextMiddleware.HeaderName, f.OrgId.ToString("D"));
        Assert.False(await client.GetFromJsonAsync<bool>("/api/public/method-test-context"));
    }

    [Fact]
    public async Task RealIdentityControllerReturnsCapabilityNamesAndKeepsTokenDistinctFromUi()
    {
        await using var guard = await MethodGenericWriteGuardTests.GuardFixture.CreateAsync();
        var f = guard.Seed;
        var issued = await f.Service().IssueAsync(f.ProjectId, new("Executor real HTTP", "agente"));
        var key = RandomNumberGenerator.GetBytes(32);
        await using var app = Host(f, key); await app.StartAsync();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        var token = await client.GetFromJsonAsync<MethodIdentityDto>("/api/method/me");
        Assert.NotNull(token); Assert.Equal("methodToken", token.AuthenticationKind); Assert.Equal("agente", token.Role);
        Assert.Equal(f.ProjectId, token.ProjectId); Assert.Equal(issued.Metadata.PrincipalId, token.PrincipalId);
        Assert.Contains("View", token.Capabilities); Assert.Contains("ChangeStatus", token.Capabilities);
        Assert.DoesNotContain("ManagePermissions", token.Capabilities);
        Assert.All(token.Capabilities, capability => Assert.False(int.TryParse(capability, out _)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HumanJwt(key));
        client.DefaultRequestHeaders.Add(OrganizationContextMiddleware.HeaderName, f.OrgId.ToString("D"));
        var human = await client.GetFromJsonAsync<MethodIdentityDto>($"/api/projects/{f.ProjectId:D}/method/me");
        Assert.NotNull(human); Assert.Equal("uiSession", human.AuthenticationKind); Assert.Equal("humano", human.Role);
        Assert.Null(human.TokenId); Assert.Null(human.PrincipalId); Assert.Contains("ManagePermissions", human.Capabilities);
    }

    private static HttpClient Client() => new() { BaseAddress = new Uri("http://127.0.0.1:3137"), Timeout = TimeSpan.FromSeconds(30) };
    private static string HumanJwt(byte[] key) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "method-http-test", audience: "method-http-test",
        claims: [new Claim(ClaimTypes.NameIdentifier, MethodTokenTests.Fixture.HumanA), new Claim(ClaimTypes.Name, "Humano")],
        expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
    private sealed record Identity(string AuthenticationKind, string Role, bool Interactive, string Channel, string AuditOrigin, string CorrelationId);
    private sealed class ProjectControllersOnly : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in feature.Controllers.Where(x => x.AsType() != typeof(MethodProjectsController)
                && x.AsType() != typeof(MethodIdentityController)).ToArray()) feature.Controllers.Remove(controller);
        }
    }
}
