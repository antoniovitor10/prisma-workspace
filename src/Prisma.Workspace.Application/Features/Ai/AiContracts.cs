using System.Text.Json;
using FluentValidation;
using MediatR;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Features.Ai;

public sealed record AiQuery(string Operation, string UserId, Guid? Id = null, JsonElement? Input = null) : IRequest<object?>;
public sealed record AiCommand(string Operation, string UserId, Guid? Id = null, JsonElement? Input = null,
    Func<string, object, Task>? Emit = null) : IRequest<object?>;
public sealed class AiQueryHandler(IAiService service) : IRequestHandler<AiQuery, object?>
{
    public Task<object?> Handle(AiQuery r, CancellationToken ct) => service.ExecuteAsync(r.Operation, r.UserId, r.Id, r.Input, null, ct);
}
public sealed class AiCommandHandler(IAiService service) : IRequestHandler<AiCommand, object?>
{
    public Task<object?> Handle(AiCommand r, CancellationToken ct) => service.ExecuteAsync(r.Operation, r.UserId, r.Id, r.Input, r.Emit, ct);
}
public sealed class AiCommandValidator : AbstractValidator<AiCommand>
{
    public AiCommandValidator() { RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Operation).NotEmpty(); }
}
public interface IAiService
{
    Task<object?> ExecuteAsync(string operation, string userId, Guid? id, JsonElement? input,
        Func<string, object, Task>? emit, CancellationToken ct);
}
public sealed class AiException(int status, string message, DateTimeOffset? resetsAt = null) : Exception(message)
{
    public int Status { get; } = status;
    public DateTimeOffset? ResetsAt { get; } = resetsAt;
}
public sealed record AiToolCall(string Id, string Name, string Arguments);
public sealed record AiProviderMessage(string Role, string Content, IReadOnlyList<AiToolCall>? Calls = null, string? ToolCallId = null);
public sealed record AiProviderResult(string Text, IReadOnlyList<AiToolCall> Calls, long InputTokens, long OutputTokens);
public interface IAiChatProvider
{
    Task<AiProviderResult> CompleteAsync(AiProviderConnection connection, string? secret,
        IReadOnlyList<AiProviderMessage> messages, bool nativeTools, int maxTokens,
        Func<string, Task>? delta, CancellationToken ct);
}
public interface IAiProviderFactory { IAiChatProvider Create(AiProviderConnection connection); }
public sealed record AiSource(string Reference, string Title, string Path);
public interface IAiWorkspaceTools
{
    Task ValidateContextAsync(JsonElement context, string userId, CancellationToken ct);
    Task<object> ExecuteAsync(string name, JsonElement arguments, string userId, JsonElement context, CancellationToken ct);
    Task<IReadOnlyList<AiSource>> ResolveSourcesAsync(string text, string userId, CancellationToken ct);
}
public interface IAiRetrieval { Task<object> SearchAsync(string query, string userId, CancellationToken ct); }
public interface IAiUsageMeter
{
    Task<AiUsageRecord> BeginAsync(AiProviderConnection connection, string userId, Guid? organizationId, int reservation, CancellationToken ct);
    Task FinishAsync(AiUsageRecord record, AiProviderConnection connection, AiProviderResult? result, int durationMs, string outcome, CancellationToken ct);
}
