using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public static class MethodIntegrationServiceExtensions
{
    public static IServiceCollection AddMethodIntegration(this IServiceCollection services)
    {
        services.AddMethodCore();
        services.AddSingleton<IMethodGitSecretStore, ConfigurationMethodGitSecretStore>();
        services.AddSingleton<GitWebhookIngressLimiter>();
        services.AddHttpClient(GitHubProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["Authorization"]);
        services.AddSingleton<IGitProvider, GitHubProvider>();
        services.AddScoped<IMethodGitStore, EfMethodGitStore>();
        services.AddScoped<GitRepositoryOperations>();
        services.AddScoped<GitPullRequestOperations>();
        services.AddScoped<GitTaskResolver>();
        services.AddScoped<GitWebhookIngress>();
        services.AddScoped<GitDeliveryProcessor>();
        services.AddScoped<IRequestHandler<AcceptGitWebhookCommand, GitDeliveryReceipt>, AcceptGitWebhookHandler>();
        services.AddScoped<IMethodProjectProvisioner, MethodProjectProvisioner>();
        services.AddScoped<IMethodProjectOperations, MethodProjectOperations>();
        services.AddScoped<IMethodTaskOperations, MethodTaskOperations>();
        services.AddScoped<IMethodMoveService, MethodMoveService>();
        services.AddHostedService<GitDeliveryWorker>();
        return services;
    }
}
