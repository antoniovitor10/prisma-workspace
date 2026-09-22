using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Me;

public sealed record MyWorkTaskDto(
    Guid Id, long Number, string Title, Guid BoardId, string BoardName,
    Guid? ProjectId, string? ProjectKey, string? StageName, string? StatusColor,
    Priority Priority, WorkItemKind Kind, WorkItemOrigin Origin,
    string? ResponsibleId, Guid? TeamId, string? TeamName, string? RequesterName,
    DateOnly? StartDate, DateOnly? DueDate, DateTimeOffset? CompletedAt,
    bool Assigned, bool Created, bool Following, bool Today, bool ThisWeek,
    bool Overdue, bool Blocked, bool ExternalRequest, bool UpcomingDeadline,
    IReadOnlyList<string> Tags,
    // Tempo ja gasto pelo usuario nesta tarefa (somente lancamentos encerrados;
    // o cronometro em andamento e somado no front, evitando contagem dupla).
    int UserTimeSeconds = 0);

public sealed record MyWorkCommentDto(
    Guid Id, Guid WorkItemId, long WorkItemNumber, string WorkItemTitle,
    string Author, string Content, DateTimeOffset CreatedAt, bool IsMention);
public sealed record MyWorkApprovalDto(
    Guid Id, Guid WorkItemId, long WorkItemNumber, string WorkItemTitle,
    string BoardName, DateTimeOffset CreatedAt);
public sealed record MyWorkNotificationDto(
    string Type, string Severity, string Title, string Message,
    Guid? WorkItemId, DateTimeOffset OccurredAt);
public sealed record MyWorkSummaryDto(
    int Assigned, int Today, int ThisWeek, int Overdue, int Blocked,
    int ExternalRequests, int PendingApprovals, int Mentions);
/// <summary>Projeto ativo em que a pessoa participa, com o recorte de trabalho dela.</summary>
public sealed record MyWorkProjectDto(
    Guid Id, string Key, string Name,
    int OpenTasks, int OverdueTasks, int CompletedTasks, int ProgressPercentage,
    Guid? CurrentSprintId, string? CurrentSprintName, int? CurrentSprintDaysLeft);

/// <summary>
/// Sprint em curso. O progresso vem em dois recortes propositalmente: o da sprint
/// inteira e o das tarefas da pessoa, para separar "a sprint vai bem" de "eu estou em dia".
/// </summary>
public sealed record MyWorkSprintDto(
    Guid Id, string Name, Guid ProjectId, string ProjectKey, string ProjectName,
    DateOnly StartDate, DateOnly EndDate, int DaysLeft, bool EndingSoon,
    int TotalItems, int TotalCompleted, int TotalProgressPercentage,
    int MyItems, int MyCompleted, int MyProgressPercentage);

/// <summary>Apontamento da semana corrente, sem qualquer métrica financeira.</summary>
public sealed record MyWorkHoursDto(
    decimal WeekHours, decimal PlannedHours, int DaysWithoutEntry);

public sealed record MyWorkDashboardDto(
    MyWorkSummaryDto Summary, IReadOnlyList<MyWorkTaskDto> Tasks,
    IReadOnlyList<MyWorkCommentDto> Comments, IReadOnlyList<MyWorkCommentDto> Mentions,
    IReadOnlyList<MyWorkApprovalDto> PendingApprovals,
    IReadOnlyList<MyWorkTaskDto> UpcomingDeadlines,
    IReadOnlyList<MyWorkNotificationDto> ImportantNotifications,
    IReadOnlyList<MyWorkProjectDto> Projects,
    IReadOnlyList<MyWorkSprintDto> Sprints,
    MyWorkHoursDto Hours);

public sealed record GetMyWorkDashboardQuery(string UserId) : IRequest<MyWorkDashboardDto>;
public sealed class GetMyWorkDashboardQueryHandler
    : IRequestHandler<GetMyWorkDashboardQuery, MyWorkDashboardDto>
{
    private readonly IMyWorkRepository _myWork;
    private readonly IApprovalRepository _approvals;
    private readonly IUserDirectory _users;
    private readonly IProjectAccessService _projectAccess;
    public GetMyWorkDashboardQueryHandler(
        IMyWorkRepository myWork, IApprovalRepository approvals, IUserDirectory users,
        IProjectAccessService projectAccess)
        => (_myWork, _approvals, _users, _projectAccess) = (myWork, approvals, users, projectAccess);

    public async Task<MyWorkDashboardDto> Handle(GetMyWorkDashboardQuery request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        var tasks = await _myWork.GetTasksAsync(request.UserId, ct);
        var comments = await _myWork.GetRecentCommentsAsync(request.UserId, user?.Email, 40, ct);
        var approvals = (await _approvals.GetByApproverAsync(request.UserId, 50, ct))
            .Where(x => x.EstaPendente).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var upcomingEnd = today.AddDays(14);

        var projetos = await _myWork.GetActiveProjectsAsync(request.UserId, ct);
        var sprints = await _myWork.GetSprintsAsync(request.UserId, ct);
        var projectIds = tasks.Select(x => x.Board.ProjectId)
            .Concat(projetos.Select(x => x.Id))
            .Concat(sprints.Select(x => x.ProjectId))
            .Concat(comments.Select(x => x.WorkItem.Board.ProjectId))
            .Concat(approvals.Select(x => x.WorkItem.Board.ProjectId))
            .Distinct()
            .ToArray();
        var accessible = await _projectAccess.GetAccessibleProjectIdsAsync(projectIds, request.UserId, ct);
        tasks = tasks.Where(x => accessible.Contains(x.Board.ProjectId)).ToList();
        projetos = projetos.Where(x => accessible.Contains(x.Id)).ToList();
        sprints = sprints.Where(x => accessible.Contains(x.ProjectId)).ToList();
        comments = comments.Where(x => accessible.Contains(x.WorkItem.Board.ProjectId)).ToList();
        approvals = approvals.Where(x => accessible.Contains(x.WorkItem.Board.ProjectId)).ToList();

        var mappedTasks = tasks.Select(item => MapTask(item, request.UserId, today, weekEnd, upcomingEnd)).ToList();
        var mappedComments = comments.Select(x => new MyWorkCommentDto(
            x.Id, x.WorkItemId, x.WorkItem.Number, x.WorkItem.Title,
            string.IsNullOrWhiteSpace(x.UserName) ? "Usuário" : x.UserName,
            x.Content, x.CreatedAt,
            !string.IsNullOrWhiteSpace(user?.Email)
                && x.Content.Contains($"@{user.Email}", StringComparison.OrdinalIgnoreCase))).ToList();
        var mentions = mappedComments.Where(x => x.IsMention).ToList();
        var mappedApprovals = approvals.Select(x => new MyWorkApprovalDto(
            x.Id, x.WorkItemId, x.WorkItem.Number, x.WorkItem.Title,
            x.WorkItem.Board.Name, x.CreatedAt)).ToList();

        return new MyWorkDashboardDto(
            new MyWorkSummaryDto(
                mappedTasks.Count(x => x.Assigned), mappedTasks.Count(x => x.Today),
                mappedTasks.Count(x => x.ThisWeek), mappedTasks.Count(x => x.Overdue),
                mappedTasks.Count(x => x.Blocked), mappedTasks.Count(x => x.ExternalRequest && x.Assigned),
                mappedApprovals.Count, mentions.Count),
            mappedTasks, mappedComments, mentions, mappedApprovals,
            mappedTasks.Where(x => x.UpcomingDeadline).OrderBy(x => x.DueDate).ToList(),
            BuildNotifications(mappedTasks, mentions, mappedApprovals, tasks),
            BuildProjects(projetos, sprints, mappedTasks, today),
            BuildSprints(sprints, request.UserId, today),
            BuildHours(tasks, request.UserId, today));
    }

    /// <summary>
    /// Projetos ativos da pessoa. Um projeto sem tarefa dela continua aparecendo, com
    /// contagem zero: sumir da lista esconderia que ela participa dele.
    /// </summary>
    private static IReadOnlyList<MyWorkProjectDto> BuildProjects(
        IReadOnlyList<Project> projetos,
        IReadOnlyList<Sprint> sprints,
        IReadOnlyList<MyWorkTaskDto> tarefas,
        DateOnly hoje)
        => projetos.Select(projeto =>
        {
            var minhas = tarefas.Where(x => x.ProjectId == projeto.Id).ToList();
            var abertas = minhas.Count(x => x.CompletedAt is null);
            var concluidas = minhas.Count(x => x.CompletedAt is not null);
            var emCurso = sprints
                .Where(x => x.ProjectId == projeto.Id && x.StatusEm(hoje) == SprintStatus.Active)
                .OrderBy(x => x.EndDate)
                .FirstOrDefault();
            return new MyWorkProjectDto(
                projeto.Id, projeto.Key, projeto.Name,
                abertas, minhas.Count(x => x.Overdue), concluidas,
                minhas.Count == 0 ? 0 : (int)Math.Round(concluidas * 100m / minhas.Count),
                emCurso?.Id, emCurso?.Name,
                emCurso is null ? null : Math.Max(0, emCurso.EndDate.DayNumber - hoje.DayNumber));
        })
        // Quem tem atraso primeiro, depois quem tem mais trabalho aberto.
        .OrderByDescending(x => x.OverdueTasks)
        .ThenByDescending(x => x.OpenTasks)
        .ThenBy(x => x.Name)
        .ToList();

    /// <summary>
    /// Sprints ativas, com progresso geral e progresso da pessoa lado a lado. Sprints
    /// encerradas ficam no histórico do projeto e não aparecem aqui.
    /// </summary>
    private static IReadOnlyList<MyWorkSprintDto> BuildSprints(
        IReadOnlyList<Sprint> sprints, string userId, DateOnly hoje)
        => sprints
            .Where(x => x.StatusEm(hoje) == SprintStatus.Active)
            .Select(sprint =>
            {
                var itens = sprint.WorkItems.Where(x => !x.IsArchived).ToList();
                var minhas = itens.Where(x => x.ResponsibleId == userId
                    || x.Assignees.Any(a => a.UserId == userId)).ToList();
                var totalConcluidos = itens.Count(x => x.CompletedAt is not null);
                var minhasConcluidas = minhas.Count(x => x.CompletedAt is not null);
                var diasRestantes = Math.Max(0, sprint.EndDate.DayNumber - hoje.DayNumber);
                return new MyWorkSprintDto(
                    sprint.Id, sprint.Name, sprint.ProjectId,
                    sprint.Project.Key, sprint.Project.Name,
                    sprint.StartDate, sprint.EndDate, diasRestantes, diasRestantes <= 2,
                    itens.Count, totalConcluidos,
                    itens.Count == 0 ? 0 : (int)Math.Round(totalConcluidos * 100m / itens.Count),
                    minhas.Count, minhasConcluidas,
                    minhas.Count == 0 ? 0 : (int)Math.Round(minhasConcluidas * 100m / minhas.Count));
            })
            .OrderBy(x => x.DaysLeft)
            .ToList();

    /// <summary>
    /// Apontamento da semana corrente da pessoa. Nunca expõe custo, valor-hora ou
    /// qualquer métrica financeira (D23 e catálogo de relatórios).
    /// </summary>
    private static MyWorkHoursDto BuildHours(
        IReadOnlyList<WorkItem> tarefas, string userId, DateOnly hoje)
    {
        var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
        var lancamentos = tarefas
            .SelectMany(x => x.TimeEntries)
            .Where(x => x.UserId == userId && x.EndedAt.HasValue)
            .Where(x => DateOnly.FromDateTime(x.StartedAt.UtcDateTime) >= inicioSemana
                && DateOnly.FromDateTime(x.StartedAt.UtcDateTime) <= hoje)
            .ToList();

        var horas = lancamentos.Sum(x => (decimal)(x.EndedAt!.Value - x.StartedAt).TotalHours);
        var previstas = tarefas
            .Where(x => x.CompletedAt is null
                && (x.ResponsibleId == userId || x.Assignees.Any(a => a.UserId == userId)))
            .Sum(x => x.EstimatedHours ?? 0);

        var diasComLancamento = lancamentos
            .Select(x => DateOnly.FromDateTime(x.StartedAt.UtcDateTime))
            .Distinct().Count();
        var diasDecorridos = hoje.DayNumber - inicioSemana.DayNumber + 1;

        return new MyWorkHoursDto(
            Math.Round(horas, 2), Math.Round(previstas, 2),
            Math.Max(0, diasDecorridos - diasComLancamento));
    }

    private static MyWorkTaskDto MapTask(
        WorkItem item, string userId, DateOnly today, DateOnly weekEnd, DateOnly upcomingEnd)
    {
        var assigned = item.ResponsibleId == userId || item.Assignees.Any(x => x.UserId == userId);
        var blocked = item.OutgoingLinks.Any(x => x.Type == WorkItemLinkType.DependsOn
                && x.TargetWorkItem.CompletedAt is null)
            || item.IncomingLinks.Any(x => x.Type == WorkItemLinkType.Blocks
                && x.SourceWorkItem.CompletedAt is null);
        var open = item.CompletedAt is null;
        return new MyWorkTaskDto(
            item.Id, item.Number, item.Title, item.BoardId, item.Board.Name,
            item.Board.ProjectId, item.Board.Project?.Key, item.Stage?.Name,
            item.WorkflowStatus?.Color, item.Priority, item.Kind, item.Origin,
            item.ResponsibleId, item.TeamId, item.Team?.Name, item.RequesterName,
            item.StartDate, item.DueDate, item.CompletedAt,
            assigned, item.CreatedBy == userId || item.RequesterId == userId,
            item.Followers.Any(x => x.UserId == userId),
            // "Meu trabalho" fala do que e SEU: prazos, atrasos e bloqueios so contam
            // quando voce e responsavel (criador/seguidor ve pela aba propria).
            assigned && open && item.DueDate == today,
            assigned && open && item.DueDate >= today && item.DueDate <= weekEnd,
            assigned && open && item.DueDate < today, assigned && open && blocked,
            item.Origin != WorkItemOrigin.Internal,
            assigned && open && item.DueDate >= today && item.DueDate <= upcomingEnd,
            item.WorkItemTags.Select(x => x.Tag.Name).ToList(),
            item.TimeEntries
                .Where(e => e.UserId == userId && e.EndedAt != null)
                .Sum(e => Math.Max(0, (int)(e.EndedAt!.Value - e.StartedAt).TotalSeconds)));
    }

    private static IReadOnlyList<MyWorkNotificationDto> BuildNotifications(
        IReadOnlyList<MyWorkTaskDto> tasks, IReadOnlyList<MyWorkCommentDto> mentions,
        IReadOnlyList<MyWorkApprovalDto> approvals,
        IReadOnlyList<WorkItem> sourceTasks)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new List<MyWorkNotificationDto>();
        result.AddRange(tasks.Where(x => x.Overdue).Take(8).Select(x => new MyWorkNotificationDto(
            "overdue", "danger", $"#{x.Number} está atrasada", x.Title, x.Id, now)));
        result.AddRange(tasks.Where(x => x.Blocked).Take(8).Select(x => new MyWorkNotificationDto(
            "blocked", "warning", $"#{x.Number} está bloqueada", x.Title, x.Id, now)));
        result.AddRange(mentions.Take(8).Select(x => new MyWorkNotificationDto(
            "mention", "info", $"Você foi mencionado em #{x.WorkItemNumber}",
            x.Content, x.WorkItemId, x.CreatedAt)));
        result.AddRange(approvals.Take(8).Select(x => new MyWorkNotificationDto(
            "approval", "info", $"Aprovação pendente em #{x.WorkItemNumber}",
            x.WorkItemTitle, x.WorkItemId, x.CreatedAt)));
        return result.OrderByDescending(x => x.OccurredAt).Take(24).ToList();
    }
}
