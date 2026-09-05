using Prisma.Workspace.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Search;

public sealed record GlobalSearchDto(
    string Query,
    IReadOnlyDictionary<string, IReadOnlyList<GlobalSearchHit>> Groups,
    int Total);

public sealed record GlobalSearchQuery(string Query, string UserId, int LimitPerGroup = 6)
    : IRequest<GlobalSearchDto>;

public sealed class GlobalSearchQueryValidator : AbstractValidator<GlobalSearchQuery>
{
    public GlobalSearchQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.LimitPerGroup).InclusiveBetween(1, 20);
    }
}

public sealed class GlobalSearchQueryHandler : IRequestHandler<GlobalSearchQuery, GlobalSearchDto>
{
    private readonly IGlobalSearchRepository _repository;
    public GlobalSearchQueryHandler(IGlobalSearchRepository repository) => _repository = repository;

    public async Task<GlobalSearchDto> Handle(GlobalSearchQuery request, CancellationToken ct)
    {
        var query = request.Query.Trim();
        var hits = await _repository.SearchAsync(query, request.LimitPerGroup, request.UserId, ct);
        var groups = hits
            .GroupBy(x => x.Kind)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<GlobalSearchHit>)x.ToList());
        return new GlobalSearchDto(query, groups, hits.Count);
    }
}
