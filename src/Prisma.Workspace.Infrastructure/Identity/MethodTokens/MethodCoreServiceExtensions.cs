using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public static class MethodCoreServiceExtensions
{
    public static IServiceCollection AddMethodCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMethodFeatureConfiguration, MethodFeatureConfiguration>();
        services.AddScoped<MethodAccessContext>();
        services.AddScoped<IMethodAccessContext>(sp => sp.GetRequiredService<MethodAccessContext>());
        services.AddScoped<IMethodContextInitializer>(sp => sp.GetRequiredService<MethodAccessContext>());
        services.AddScoped<MethodPermissionEvaluator>();
        services.AddScoped<IMethodPermissionEvaluator>(sp => sp.GetRequiredService<MethodPermissionEvaluator>());
        services.AddScoped<IMethodTokenService, MethodTokenService>();
        services.AddScoped<IMethodWorkerContextInitializer, MethodWorkerContextInitializer>();
        // IMethodGitStore é registrado com a camada B na integração; só é resolvido ao usar a política.
        services.AddScoped<IMethodTransitionPolicy, MethodTransitionPolicy>();
        return services;
    }
}
