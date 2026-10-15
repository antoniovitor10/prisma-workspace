using MediatR;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

public sealed record UpdateTimeEntryCommand(Guid Id, string UserId, DateTimeOffset StartedAt,
    DateTimeOffset EndedAt, string? Note) : IRequest<TimeEntryDto>;

public sealed class UpdateTimeEntryCommandHandler(ITimeEntryRepository entries, IWorkItemAccessService access)
    : IRequestHandler<UpdateTimeEntryCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(UpdateTimeEntryCommand request, CancellationToken ct)
    {
        var entry = await entries.GetByIdAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Lançamento");
        if (entry.UserId != request.UserId)
            throw new AcessoNegadoException("Você só pode alterar seus próprios lançamentos.");
        await access.EnsureAsync(entry.WorkItemId, request.UserId, PlatformPermission.Edit, ProjectRole.Member, ct);
        DomainException.Garantir(!entry.EstaAberto, "Pare o cronômetro antes de alterar o lançamento.");
        DomainException.Garantir(request.EndedAt > request.StartedAt, "O fim deve ser maior que o início.");
        DomainException.Garantir(request.EndedAt <= DateTimeOffset.UtcNow, "O lançamento não pode estar no futuro.");
        DomainException.Garantir(request.Note?.Length <= 2000 || request.Note is null, "A observação deve ter até 2000 caracteres.");
        DomainException.Garantir(!await entries.HasOverlapAsync(request.UserId, entry.Id, request.StartedAt, request.EndedAt, ct),
            "O período se sobrepõe a outro lançamento seu.");
        entry.Correct(request.StartedAt, request.EndedAt, request.Note);
        await entries.UpdateAsync(entry, ct);
        return entry.ToDto();
    }
}
