using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

public interface IMethodAccessContext
{
    MethodActorIdentity? Current { get; }
}
