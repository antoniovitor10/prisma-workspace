using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Detran.Kanban.Application.Features.Audit;

public sealed record AuditLogDto(
    Guid Id,
    string? UserId,
    DateTimeOffset OccurredAt,
    string Action,
    string EntityType,
    string EntityId,
    string? PreviousValuesJson,
    string? NewValuesJson,
    string Origin,
    string? IpAddress,
    string? CorrelationId);

public sealed record AuditPageDto(IReadOnlyList<AuditLogDto> Items, int Total, int Page, int PageSize);

public sealed record SearchAuditLogsQuery(
    string ActorId,
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    string? UserId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<AuditPageDto>;

public sealed class SearchAuditLogsQueryValidator : AbstractValidator<SearchAuditLogsQuery>
{
    public SearchAuditLogsQueryValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.EntityType).MaximumLength(120);
        RuleFor(x => x.EntityId).MaximumLength(500);
        RuleFor(x => x.Action).MaximumLength(40);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}

public sealed class SearchAuditLogsQueryHandler : IRequestHandler<SearchAuditLogsQuery, AuditPageDto>
{
    private readonly IAuditLogRepository _repository;
    private readonly IPermissionService _permissions;
    public SearchAuditLogsQueryHandler(IAuditLogRepository repository, IPermissionService permissions)
        => (_repository, _permissions) = (repository, permissions);

    public async Task<AuditPageDto> Handle(SearchAuditLogsQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.AdministerOrganization,
            PermissionScope.Organization, cancellationToken: ct);
        var criteria = new AuditSearchCriteria(request.EntityType, request.EntityId, request.Action,
            request.UserId, request.From, request.To, request.Page, request.PageSize);
        var result = await _repository.SearchAsync(criteria, ct);
        return new AuditPageDto(result.Items.Select(x => new AuditLogDto(
            x.Id, x.UserId, x.OccurredAt, x.Action, x.EntityType, x.EntityId,
            x.PreviousValuesJson, x.NewValuesJson, x.Origin, x.IpAddress, x.CorrelationId)).ToList(),
            result.Total, request.Page, request.PageSize);
    }
}
