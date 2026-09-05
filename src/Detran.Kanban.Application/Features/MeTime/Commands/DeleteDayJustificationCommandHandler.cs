using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Commands;

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
