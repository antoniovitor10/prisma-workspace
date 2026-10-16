using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Features.MethodSecurity;

public interface IMethodTaskOperations
{
    Task<IReadOnlyList<MethodTaskDto>> ListAsync(Guid projectId, string? status, bool next, CancellationToken ct);
    Task<MethodTaskDto> GetAsync(Guid projectId, string id, CancellationToken ct);
    Task<MethodTaskDto> CreateAsync(Guid projectId, CreateMethodTaskRequest request, CancellationToken ct);
    Task<MethodTaskDto> UpdateAsync(Guid projectId, string id, UpdateMethodTaskRequest request, CancellationToken ct);
    Task<MethodTaskDto> MoveAsync(Guid projectId, string id, MoveMethodTaskRequest request, CancellationToken ct);
    Task<MethodCommentDto> CommentAsync(Guid projectId, string id, MethodCommentRequest request, CancellationToken ct);
    Task<ResumeMethodTaskApprovalDto> ApproveResumeAsync(Guid projectId, Guid itemId, ResumeMethodTaskRequest request, CancellationToken ct);
}
