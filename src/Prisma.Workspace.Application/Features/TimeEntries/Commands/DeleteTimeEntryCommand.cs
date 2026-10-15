using MediatR;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

public sealed record DeleteTimeEntryCommand(Guid Id, string UserId) : IRequest;

public sealed class DeleteTimeEntryCommandHandler(ITimeEntryRepository entries, IWorkItemAccessService access)
    : IRequestHandler<DeleteTimeEntryCommand>
{
    public async Task Handle(DeleteTimeEntryCommand request, CancellationToken ct)
    {
        var entry = await entries.GetByIdAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Lançamento");
        if (entry.UserId != request.UserId)
            throw new AcessoNegadoException("Você só pode excluir seus próprios lançamentos.");
        await access.EnsureAsync(entry.WorkItemId, request.UserId, PlatformPermission.Edit, ProjectRole.Member, ct);
        DomainException.Garantir(!entry.EstaAberto, "Pare o cronômetro antes de excluir o lançamento.");
        await entries.DeleteAsync(entry, ct);
    }
}
