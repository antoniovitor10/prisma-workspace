using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.MethodSecurity;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public sealed class MethodFeatureConfiguration(IConfiguration configuration) : IMethodFeatureConfiguration
{
    public bool Enabled => configuration.GetValue<bool>("MethodGit:Enabled");
}
