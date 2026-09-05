using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Commands;

public class DeleteDayJustificationCommandHandler : IRequestHandler<DeleteDayJustificationCommand>
{
    private readonly IDayJustificationRepository _justifications;

    public DeleteDayJustificationCommandHandler(IDayJustificationRepository justifications)
    {
        _justifications = justifications;
    }

    public async Task Handle(DeleteDayJustificationCommand request, CancellationToken cancellationToken)
    {
        var entity = await _justifications.GetByIdForUserAsync(request.Id, request.UserId, cancellationToken)
            ?? throw new NaoEncontradoException("Justificativa");
        await _justifications.DeleteAsync(entity, cancellationToken);
    }
}
