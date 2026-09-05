using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems;

public sealed record WorkItemSummaryDto(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    long Number,
    string Title,
    string Status);

public sealed record SearchWorkItemsQuery(
    Guid ProjectId,
    string Query,
    string UserId,
    int Limit = 12) : IRequest<IReadOnlyList<WorkItemSummaryDto>>;

public sealed class SearchWorkItemsQueryValidator : AbstractValidator<SearchWorkItemsQuery>
{
    public SearchWorkItemsQueryValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.Query).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, 20);
    }
}

public sealed class SearchWorkItemsQueryHandler
    : IRequestHandler<SearchWorkItemsQuery, IReadOnlyList<WorkItemSummaryDto>>
{
    private readonly IWorkItemSearchRepository _search;
    private readonly IProjectAccessService _access;

    public SearchWorkItemsQueryHandler(
        IWorkItemSearchRepository search,
        IProjectAccessService access)
        => (_search, _access) = (search, access);

    public async Task<IReadOnlyList<WorkItemSummaryDto>> Handle(
        SearchWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAtLeastAsync(
            request.ProjectId, request.UserId, ProjectRole.Member, cancellationToken);
        return (await _search.SearchVisibleAsync(
                request.Query.Trim(), request.UserId, request.Limit, cancellationToken))
            .Select(item => new WorkItemSummaryDto(
                item.Id, item.ProjectId, item.ProjectKey, item.Number,
                item.Title, item.Status))
            .ToList();
    }
}
