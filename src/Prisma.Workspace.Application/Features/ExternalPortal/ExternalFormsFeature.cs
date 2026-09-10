using System.Globalization;
using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;
using ExternalPortalEntity = Prisma.Workspace.Domain.Entities.ExternalPortal;

namespace Prisma.Workspace.Application.Features.ExternalPortal;

public record GetProjectExternalFormsQuery(Guid ProjectId, string ActorId)
    : IRequest<IReadOnlyList<ExternalFormDto>>;

public class GetProjectExternalFormsQueryHandler
    : IRequestHandler<GetProjectExternalFormsQuery, IReadOnlyList<ExternalFormDto>>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectAccessService _access;

    public GetProjectExternalFormsQueryHandler(
        IExternalPortalRepository portals, IProjectAccessService access)
        => (_portals, _access) = (portals, access);

    public async Task<IReadOnlyList<ExternalFormDto>> Handle(
        GetProjectExternalFormsQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        var portal = await _portals.GetByProjectAsync(request.ProjectId, ct);
        if (portal is null) return [];
        return portal.Forms.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Title)
            .Select(ExternalFormMapper.Detail).ToList();
    }
}

public record GetPublicExternalFormQuery(string PortalSlug, string FormSlug) : IRequest<ExternalFormDto>;

public class GetPublicExternalFormQueryHandler : IRequestHandler<GetPublicExternalFormQuery, ExternalFormDto>
{
    private readonly IExternalPortalRepository _portals;
    public GetPublicExternalFormQueryHandler(IExternalPortalRepository portals) => _portals = portals;

    public async Task<ExternalFormDto> Handle(GetPublicExternalFormQuery request, CancellationToken ct)
    {
        var form = await _portals.GetPublicFormAsync(
            ExternalPortalEntity.NormalizeSlug(request.PortalSlug),
            ExternalPortalEntity.NormalizeSlug(request.FormSlug), ct)
            ?? throw new NaoEncontradoException("Formulário externo");
        return ExternalFormMapper.Detail(form);
    }
}

public record SaveExternalFormCommand(
    Guid ProjectId,
    Guid? FormId,
    string PublicSlug,
    string Title,
    string? Description,
    string? Category,
    string? ConfirmationMessage,
    bool IsEnabled,
    bool IsDefault,
    Priority DefaultPriority,
    Guid? InitialStageId,
    Guid? DefaultTeamId,
    string? DefaultResponsibleId,
    int MaxFiles,
    long MaxFileSizeBytes,
    string AllowedExtensions,
    string AllowedMimeTypes,
    int MinimumCompletionSeconds,
    IReadOnlyList<ExternalFormFieldDto> Fields,
    IReadOnlyList<ExternalFormAssignmentRuleDto> AssignmentRules,
    string ActorId) : IRequest<ExternalFormDto>;

public class SaveExternalFormCommandValidator : AbstractValidator<SaveExternalFormCommand>
{
    public SaveExternalFormCommandValidator()
    {
        RuleFor(x => x.PublicSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).MaximumLength(120);
        RuleFor(x => x.ConfirmationMessage).MaximumLength(1000);
        RuleFor(x => x.MaxFiles).InclusiveBetween(0, 20);
        RuleFor(x => x.MaxFileSizeBytes).InclusiveBetween(100_000, 50_000_000);
        RuleFor(x => x.MinimumCompletionSeconds).InclusiveBetween(0, 60);
        RuleFor(x => x.Fields).NotNull().Must(x => x.Count is >= 1 and <= 50)
            .WithMessage("O formulário deve possuir entre 1 e 50 campos.");
        RuleFor(x => x.AssignmentRules).NotNull().Must(x => x.Count <= 30)
            .WithMessage("O formulário aceita no máximo 30 regras de atribuição.");
    }
}

public class SaveExternalFormCommandHandler : IRequestHandler<SaveExternalFormCommand, ExternalFormDto>
{
    private static readonly Regex KeyPattern = new("^[a-z][a-zA-Z0-9_]{1,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex ExtensionPattern = new("^\\.[a-z0-9]{1,10}$", RegexOptions.CultureInvariant);
    private static readonly Regex MimePattern = new("^[a-z0-9][a-z0-9.+-]*/[a-z0-9][a-z0-9.+-]*$", RegexOptions.CultureInvariant);
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;

    public SaveExternalFormCommandHandler(
        IExternalPortalRepository portals,
        IProjectRepository projects,
        IProjectAccessService access,
        IUserDirectory users)
        => (_portals, _projects, _access, _users) = (portals, projects, access, users);

    public async Task<ExternalFormDto> Handle(SaveExternalFormCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var portal = await _portals.GetByProjectAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Portal externo");

        ValidateFields(request.Fields, project);
        await ValidateRoutingAsync(request, project, portal, ct);
        var slug = ExternalPortalEntity.NormalizeSlug(request.PublicSlug);
        DomainException.Garantir(!await _portals.FormSlugExistsAsync(portal.Id, slug, request.FormId, ct),
            "Este endereço de formulário já está em uso no portal.");

        var extensions = NormalizeList(request.AllowedExtensions, ExtensionPattern, "extensão");
        var mimeTypes = NormalizeList(request.AllowedMimeTypes, MimePattern, "tipo MIME");
        DomainException.Garantir(request.MaxFiles == 0 || extensions.Length > 0,
            "Informe ao menos uma extensão permitida quando anexos estiverem habilitados.");
        DomainException.Garantir(request.MaxFiles == 0 || mimeTypes.Length > 0,
            "Informe ao menos um tipo MIME permitido quando anexos estiverem habilitados.");

        ExternalForm form;
        if (request.FormId.HasValue)
        {
            form = await _portals.GetFormAsync(request.ProjectId, request.FormId.Value, ct)
                ?? throw new NaoEncontradoException("Formulário externo");
        }
        else
        {
            form = new ExternalForm
            {
                Id = Guid.NewGuid(),
                ExternalPortalId = portal.Id,
                ExternalPortal = portal,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _portals.AddForm(form);
            portal.Forms.Add(form);
        }

        var mustBeDefault = request.IsDefault || portal.Forms.All(x => x.Id == form.Id || !x.IsDefault);
        if (mustBeDefault)
            foreach (var sibling in portal.Forms.Where(x => x.Id != form.Id)) sibling.IsDefault = false;

        form.Configure(slug, request.Title, request.Description, request.Category,
            request.ConfirmationMessage, request.IsEnabled, mustBeDefault,
            request.DefaultPriority, request.InitialStageId, request.DefaultTeamId,
            request.DefaultResponsibleId, request.MaxFiles, request.MaxFileSizeBytes,
            string.Join(',', extensions), string.Join(',', mimeTypes), request.MinimumCompletionSeconds,
            ExternalFormSerialization.Serialize(request.Fields.OrderBy(x => x.Position)),
            ExternalFormSerialization.Serialize(request.AssignmentRules.OrderBy(x => x.Position)));

        await _portals.SaveAsync(ct);
        return ExternalFormMapper.Detail(form);
    }

    private static void ValidateFields(IReadOnlyList<ExternalFormFieldDto> fields, Project project)
    {
        DomainException.Garantir(fields.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == fields.Count,
            "As chaves dos campos não podem se repetir.");
        DomainException.Garantir(fields.Where(x => x.Kind != ExternalFormFieldKind.Custom)
            .Select(x => x.Kind).Distinct().Count() == fields.Count(x => x.Kind != ExternalFormFieldKind.Custom),
            "Cada campo padrão pode aparecer apenas uma vez.");
        DomainException.Garantir(fields.Any(x => x.Kind == ExternalFormFieldKind.Subject),
            "Inclua o campo Assunto no formulário.");

        var keys = fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            DomainException.Garantir(KeyPattern.IsMatch(field.Key),
                $"A chave '{field.Key}' deve começar com letra minúscula e conter apenas letras, números ou sublinhado.");
            DomainException.Garantir(!string.IsNullOrWhiteSpace(field.Label) && field.Label.Trim().Length <= 120,
                $"O campo '{field.Key}' precisa de um rótulo de até 120 caracteres.");
            DomainException.Garantir((!field.MinLength.HasValue || field.MinLength.Value >= 0)
                && (!field.MaxLength.HasValue || field.MaxLength.Value >= 1)
                && (!field.MinLength.HasValue || !field.MaxLength.HasValue || field.MinLength <= field.MaxLength),
                $"Os limites do campo '{field.Key}' são inválidos.");
            if (field.Type == ExternalFormFieldType.Select)
                DomainException.Garantir(field.Options is { Count: > 0 },
                    $"O campo de seleção '{field.Key}' precisa de opções.");
            if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
            {
                try { _ = new Regex(field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { throw new DomainException($"A expressão de validação de '{field.Key}' é inválida."); }
            }
            if (!string.IsNullOrWhiteSpace(field.ConditionalFieldKey))
                DomainException.Garantir(keys.Contains(field.ConditionalFieldKey)
                    && !string.Equals(field.Key, field.ConditionalFieldKey, StringComparison.OrdinalIgnoreCase),
                    $"A condição do campo '{field.Key}' referencia um campo inválido.");
            if (field.ProjectCustomFieldId.HasValue)
                DomainException.Garantir(project.CustomFields.Any(x => x.Id == field.ProjectCustomFieldId),
                    $"O campo personalizado associado a '{field.Key}' não pertence ao projeto.");
        }
    }

    private async Task ValidateRoutingAsync(
        SaveExternalFormCommand request, Project project, ExternalPortalEntity portal, CancellationToken ct)
    {
        var stageIds = portal.Project.Stages.Select(x => x.Id).ToHashSet();
        var teamIds = project.Teams.Select(x => x.TeamId).ToHashSet();
        var memberIds = project.Members.Select(x => x.UserId).Append(project.OwnerId).ToHashSet();
        DomainException.Garantir(!request.InitialStageId.HasValue || stageIds.Contains(request.InitialStageId.Value),
            "A etapa inicial não pertence ao quadro de entrada.");
        DomainException.Garantir(!request.DefaultTeamId.HasValue || teamIds.Contains(request.DefaultTeamId.Value),
            "A equipe padrão não pertence ao projeto.");
        if (!string.IsNullOrWhiteSpace(request.DefaultResponsibleId))
        {
            DomainException.Garantir(memberIds.Contains(request.DefaultResponsibleId),
                "O responsável padrão não é membro do projeto.");
            DomainException.Garantir(await _users.GetByIdAsync(request.DefaultResponsibleId, ct) is not null,
                "O responsável padrão não existe.");
        }

        var fieldKeys = request.Fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in request.AssignmentRules)
        {
            DomainException.Garantir(rule.Operator == ExternalFormRuleOperator.Always || fieldKeys.Contains(rule.FieldKey),
                "Uma regra de atribuição referencia um campo inexistente.");
            DomainException.Garantir(rule.Operator == ExternalFormRuleOperator.Always
                || !string.IsNullOrWhiteSpace(rule.ExpectedValue),
                "Regras condicionais precisam de um valor esperado.");
            DomainException.Garantir(!rule.StageId.HasValue || stageIds.Contains(rule.StageId.Value),
                "Uma regra aponta para uma etapa de outro quadro.");
            DomainException.Garantir(!rule.TeamId.HasValue || teamIds.Contains(rule.TeamId.Value),
                "Uma regra aponta para uma equipe de outro projeto.");
            DomainException.Garantir(string.IsNullOrWhiteSpace(rule.ResponsibleId) || memberIds.Contains(rule.ResponsibleId),
                "Uma regra aponta para um responsável que não é membro do projeto.");
        }
    }

    private static string[] NormalizeList(string value, Regex pattern, string itemName)
    {
        var values = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct().ToArray();
        DomainException.Garantir(values.All(pattern.IsMatch), $"Há {itemName} inválido na configuração de anexos.");
        return values;
    }
}

public record ExternalFormSubmissionFile(
    string FileName, string? MimeType, long Length, Stream Content);

public record SubmitExternalFormCommand(
    string PortalSlug,
    string FormSlug,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyList<ExternalFormSubmissionFile> Files,
    DateTimeOffset StartedAt,
    string? Website,
    string? InvitationToken,
    string? VerificationCode,
    string? AuthenticatedUserId,
    string? AuthenticatedEmail) : IRequest<CreatedExternalRequestDto>;

public class SubmitExternalFormCommandValidator : AbstractValidator<SubmitExternalFormCommand>
{
    public SubmitExternalFormCommandValidator()
    {
        RuleFor(x => x.PortalSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.FormSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Values).NotNull().Must(x => x.Count <= 50)
            .WithMessage("O envio aceita no máximo 50 campos.");
        RuleFor(x => x.Files).NotNull().Must(x => x.Count <= 20)
            .WithMessage("O envio aceita no máximo 20 anexos.");
    }
}

public class SubmitExternalFormCommandHandler
    : IRequestHandler<SubmitExternalFormCommand, CreatedExternalRequestDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IFileStorage _storage;
    private readonly IPortalEmailSender _emailSender;
    private readonly IPlatformNotificationPublisher? _notifications;

    public SubmitExternalFormCommandHandler(
        IExternalPortalRepository portals,
        IFileStorage storage,
        IPortalEmailSender emailSender,
        IPlatformNotificationPublisher? notifications = null)
        => (_portals, _storage, _emailSender, _notifications)
            = (portals, storage, emailSender, notifications);

    public async Task<CreatedExternalRequestDto> Handle(SubmitExternalFormCommand request, CancellationToken ct)
    {
        DomainException.Garantir(string.IsNullOrWhiteSpace(request.Website), "Envio identificado como spam.");
        var form = await _portals.GetPublicFormAsync(
            ExternalPortalEntity.NormalizeSlug(request.PortalSlug),
            ExternalPortalEntity.NormalizeSlug(request.FormSlug), ct)
            ?? throw new NaoEncontradoException("Formulário externo");
        var portal = form.ExternalPortal;
        var elapsed = DateTimeOffset.UtcNow - request.StartedAt.ToUniversalTime();
        DomainException.Garantir(elapsed.TotalSeconds >= form.MinimumCompletionSeconds && elapsed.TotalHours <= 2,
            "Recarregue o formulário e tente novamente.");

        var fields = ExternalFormSerialization.Fields(form).OrderBy(x => x.Position).ToList();
        var values = NormalizeAndValidateValues(fields, request.Values, request.Files.Count);
        foreach (var field in fields.Where(x => x.ProjectCustomFieldId.HasValue))
        {
            var definition = portal.Project.CustomFields.First(x => x.Id == field.ProjectCustomFieldId);
            values.TryGetValue(field.Key, out var customValue);
            CustomFieldValueRules.Validate(definition, customValue, portal.Project);
        }
        var requesterEmail = Value(fields, values, ExternalFormFieldKind.RequesterEmail)?.Trim().ToLowerInvariant() ?? string.Empty;
        await EnsurePortalAccessAsync(portal, requesterEmail, request, ct);
        ValidateFiles(form, request.Files);

        var priority = ParsePriority(Value(fields, values, ExternalFormFieldKind.InformedPriority))
            ?? form.DefaultPriority;
        var stageId = form.InitialStageId;
        var teamId = form.DefaultTeamId ?? portal.Board.TeamId;
        var responsibleId = form.DefaultResponsibleId;
        foreach (var rule in ExternalFormSerialization.Rules(form).OrderBy(x => x.Position))
        {
            if (!RuleMatches(rule, values)) continue;
            priority = rule.Priority ?? priority;
            stageId = rule.StageId ?? stageId;
            teamId = rule.TeamId ?? teamId;
            responsibleId = string.IsNullOrWhiteSpace(rule.ResponsibleId) ? responsibleId : rule.ResponsibleId;
        }

        var initialStatus = portal.Project.WorkflowStatuses.OrderBy(x => x.Position)
            .FirstOrDefault(x => x.IsInitial);
        var initialStage = stageId.HasValue
            ? portal.Project.Stages.FirstOrDefault(x => x.Id == stageId)
            : portal.Project.Stages.OrderBy(x => x.Position)
                .FirstOrDefault(x => initialStatus == null || x.WorkflowStatusId == initialStatus.Id)
                ?? portal.Project.Stages.OrderBy(x => x.Position).FirstOrDefault();
        DomainException.Garantir(!stageId.HasValue || initialStage is not null,
            "A fila configurada para este formulário não está disponível.");

        var now = DateTimeOffset.UtcNow;
        var backlogRank = await _portals.GetNextBacklogRankAsync(portal.BoardId, ct);
        var title = Value(fields, values, ExternalFormFieldKind.Subject);
        var description = Value(fields, values, ExternalFormFieldKind.DetailedDescription);
        var requesterName = Value(fields, values, ExternalFormFieldKind.RequesterName);
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(), BoardId = portal.BoardId, TeamId = teamId,
            StageId = initialStage?.Id, WorkflowStatusId = initialStage?.WorkflowStatusId ?? initialStatus?.Id,
            Title = string.IsNullOrWhiteSpace(title) ? $"Solicitação via {form.Title}" : title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority, Kind = WorkItemKind.Request, Origin = WorkItemOrigin.ExternalPortal,
            ResponsibleId = responsibleId, RequesterId = request.AuthenticatedUserId,
            RequesterName = string.IsNullOrWhiteSpace(requesterName) ? "Solicitante" : requesterName.Trim(),
            RequesterEmail = requesterEmail, CreatedBy = request.AuthenticatedUserId,
            Position = (double)backlogRank, BacklogRank = backlogRank,
            CreatedAt = now, UpdatedAt = now
        };
        foreach (var field in fields.Where(x => x.ProjectCustomFieldId.HasValue))
            if (values.TryGetValue(field.Key, out var customValue) && !string.IsNullOrWhiteSpace(customValue))
                workItem.CustomFieldValues.Add(new WorkItemCustomFieldValue
                {
                    WorkItemId = workItem.Id,
                    FieldDefinitionId = field.ProjectCustomFieldId!.Value,
                    Value = customValue,
                    UpdatedBy = request.AuthenticatedUserId ?? "external-form",
                    UpdatedAt = now
                });

        var accessKey = PortalSecurity.GenerateAccessKey();
        var externalRequest = new ExternalRequest
        {
            Id = Guid.NewGuid(), ExternalPortalId = portal.Id, ExternalFormId = form.Id,
            WorkItemId = workItem.Id, AccessKeyHash = PortalSecurity.Hash(accessKey),
            RequesterEmail = requesterEmail,
            RequesterPhone = Value(fields, values, ExternalFormFieldKind.RequesterPhone),
            Category = Value(fields, values, ExternalFormFieldKind.Category) ?? form.Category,
            RelatedService = Value(fields, values, ExternalFormFieldKind.RelatedService),
            SubmittedValuesJson = ExternalFormSerialization.Serialize(values),
            TriageStatus = ExternalRequestTriageStatus.New, CreatedAt = now, UpdatedAt = now
        };
        externalRequest.TriageEvents.Add(new ExternalRequestTriageEvent
        {
            Id = Guid.NewGuid(), ExternalRequestId = externalRequest.Id,
            Action = ExternalRequestTriageAction.Submitted,
            ActorId = request.AuthenticatedUserId ?? $"external:{requesterEmail}",
            ActorName = workItem.RequesterName ?? "Solicitante",
            Description = "Solicitação recebida e convertida em tarefa interna.", CreatedAt = now
        });

        var storedPaths = new List<string>();
        try
        {
            foreach (var file in request.Files)
            {
                var id = Guid.NewGuid();
                var fileName = Path.GetFileName(file.FileName);
                var storedFileName = $"{id:N}{Path.GetExtension(fileName).ToLowerInvariant()}";
                var path = await _storage.SaveAsync(workItem.Id.ToString("N"), storedFileName, file.Content, ct);
                storedPaths.Add(path);
                workItem.Attachments.Add(new Attachment
                {
                    Id = id, WorkItemId = workItem.Id, StoragePath = path,
                    FileName = fileName, FileSize = file.Length,
                    MimeType = file.MimeType?.ToLowerInvariant() ?? "application/octet-stream",
                    UploadedBy = $"external:{requesterEmail}", IsExternalVisible = true, CreatedAt = now
                });
            }
            await _portals.CreateRequestAsync(workItem, externalRequest, ct);
        }
        catch
        {
            foreach (var path in storedPaths) await _storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        if (_notifications is not null)
        {
            var recipients = portal.Project.Members.Select(x => x.UserId)
                .Append(portal.Project.OwnerId)
                .Append(responsibleId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
            await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                portal.OrganizationId, userId, NotificationType.ExternalRequestReceived,
                "Nova solicitação recebida",
                $"{externalRequest.Protocol} · {workItem.Title}", $"/requests?request={externalRequest.Id}",
                workItem.Id, externalRequest.Id, portal.ProjectId)), ct);
        }

        var trackingPath = $"/portal/{portal.PublicSlug}/acompanhar?protocol={Uri.EscapeDataString(externalRequest.Protocol)}&key={accessKey}";
        var delivered = false;
        try
        {
            delivered = await _emailSender.SendRequestConfirmationAsync(
                requesterEmail, portal.Project.Name, externalRequest.Protocol, accessKey,
                trackingPath, form.ConfirmationMessage, ct);
        }
        catch
        {
            // O protocolo já foi persistido; indisponibilidade de SMTP não desfaz a solicitação.
        }
        return new CreatedExternalRequestDto(externalRequest.Protocol, accessKey, trackingPath,
            workItem.Id, form.ConfirmationMessage, delivered);
    }

    private async Task EnsurePortalAccessAsync(
        ExternalPortalEntity portal, string email, SubmitExternalFormCommand request, CancellationToken ct)
    {
        var hasAccess = !portal.RequiresAuthentication;
        ExternalPortalInvitation? invitation = null;
        ExternalPortalVerification? verification = null;
        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.Login)
            && !string.IsNullOrWhiteSpace(request.AuthenticatedUserId))
            hasAccess = string.IsNullOrWhiteSpace(request.AuthenticatedEmail)
                || string.Equals(email, PortalSecurity.NormalizeEmail(request.AuthenticatedEmail), StringComparison.Ordinal);
        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.Invitation)
            && !string.IsNullOrWhiteSpace(request.InvitationToken))
        {
            invitation = await _portals.GetInvitationAsync(
                portal.Id, email, PortalSecurity.Hash(request.InvitationToken), ct);
            hasAccess = invitation is not null;
        }
        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.EmailCode)
            && !string.IsNullOrWhiteSpace(request.VerificationCode))
        {
            verification = await _portals.GetVerificationAsync(
                portal.Id, email, PortalSecurity.Hash(request.VerificationCode), ct);
            hasAccess = verification is not null;
        }
        DomainException.Garantir(hasAccess,
            "Autentique-se, use um convite válido ou confirme o código enviado por e-mail.");
        var now = DateTimeOffset.UtcNow;
        if (invitation is not null) invitation.UsedAt = now;
        if (verification is not null) verification.UsedAt = now;
    }

    private static Dictionary<string, string?> NormalizeAndValidateValues(
        IReadOnlyList<ExternalFormFieldDto> fields,
        IReadOnlyDictionary<string, string?> submitted,
        int fileCount)
    {
        var source = new Dictionary<string, string?>(submitted, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            source.TryGetValue(field.Key, out var value);
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            var visible = string.IsNullOrWhiteSpace(field.ConditionalFieldKey)
                || source.TryGetValue(field.ConditionalFieldKey, out var conditionValue)
                && string.Equals(conditionValue?.Trim(), field.ConditionalValue?.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!visible) continue;
            if (field.Kind == ExternalFormFieldKind.Attachments)
            {
                DomainException.Garantir(!field.IsRequired || fileCount > 0,
                    $"O campo '{field.Label}' é obrigatório.");
                continue;
            }
            DomainException.Garantir(!field.IsRequired || value is not null,
                $"O campo '{field.Label}' é obrigatório.");
            if (value is null) { result[field.Key] = null; continue; }
            DomainException.Garantir(!field.MinLength.HasValue || value.Length >= field.MinLength,
                $"O campo '{field.Label}' é menor que o permitido.");
            DomainException.Garantir(!field.MaxLength.HasValue || value.Length <= field.MaxLength,
                $"O campo '{field.Label}' excede o limite permitido.");
            DomainException.Garantir(field.Type != ExternalFormFieldType.Email
                || System.Net.Mail.MailAddress.TryCreate(value, out _),
                $"O campo '{field.Label}' deve conter um e-mail válido.");
            DomainException.Garantir(field.Type != ExternalFormFieldType.Number
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                $"O campo '{field.Label}' deve conter um número válido.");
            DomainException.Garantir(field.Type != ExternalFormFieldType.Date
                || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                $"O campo '{field.Label}' deve conter uma data válida.");
            DomainException.Garantir(field.Type != ExternalFormFieldType.Select
                || field.Options?.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) == true,
                $"A opção informada em '{field.Label}' é inválida.");
            if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
            {
                try
                {
                    DomainException.Garantir(Regex.IsMatch(value, field.ValidationPattern,
                            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
                        $"O campo '{field.Label}' não atende ao formato esperado.");
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new DomainException($"Não foi possível validar o campo '{field.Label}'.");
                }
            }
            result[field.Key] = value;
        }
        return result;
    }

    private static string? Value(
        IEnumerable<ExternalFormFieldDto> fields,
        IReadOnlyDictionary<string, string?> values,
        ExternalFormFieldKind kind)
    {
        var key = fields.FirstOrDefault(x => x.Kind == kind)?.Key;
        return key is not null && values.TryGetValue(key, out var value) ? value : null;
    }

    private static bool RuleMatches(
        ExternalFormAssignmentRuleDto rule, IReadOnlyDictionary<string, string?> values)
    {
        if (rule.Operator == ExternalFormRuleOperator.Always) return true;
        values.TryGetValue(rule.FieldKey, out var actual);
        return rule.Operator switch
        {
            ExternalFormRuleOperator.Equals => string.Equals(
                actual, rule.ExpectedValue, StringComparison.OrdinalIgnoreCase),
            ExternalFormRuleOperator.Contains => actual?.Contains(
                rule.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };
    }

    private static Priority? ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var number) && Enum.IsDefined(typeof(Priority), number))
            return (Priority)number;
        return value.Trim().ToLowerInvariant() switch
        {
            "baixa" or "low" => Priority.Low,
            "média" or "media" or "medium" => Priority.Medium,
            "alta" or "high" => Priority.High,
            "crítica" or "critica" or "critical" => Priority.Critical,
            _ => null
        };
    }

    private static void ValidateFiles(ExternalForm form, IReadOnlyList<ExternalFormSubmissionFile> files)
    {
        DomainException.Garantir(files.Count <= form.MaxFiles,
            $"Este formulário aceita no máximo {form.MaxFiles} anexo(s).");
        var extensions = form.AllowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mimeTypes = form.AllowedMimeTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FileName);
            DomainException.Garantir(fileName == file.FileName && fileName.Length is > 0 and <= 255,
                "Nome de anexo inválido.");
            DomainException.Garantir(file.Length is > 0 && file.Length <= form.MaxFileSizeBytes,
                $"O arquivo '{fileName}' excede o limite configurado.");
            DomainException.Garantir(extensions.Contains(Path.GetExtension(fileName)),
                $"A extensão do arquivo '{fileName}' não é permitida.");
            DomainException.Garantir(!string.IsNullOrWhiteSpace(file.MimeType) && mimeTypes.Contains(file.MimeType),
                $"O tipo do arquivo '{fileName}' não é permitido.");
        }
    }
}
