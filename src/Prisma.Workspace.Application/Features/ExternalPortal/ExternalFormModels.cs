using System.Text.Json;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.ExternalPortal;

public record ExternalFormFieldDto(
    string Key,
    string Label,
    ExternalFormFieldType Type,
    ExternalFormFieldKind Kind,
    bool IsRequired,
    int Position,
    string? Placeholder,
    string? HelpText,
    IReadOnlyList<string>? Options,
    string? ValidationPattern,
    int? MinLength,
    int? MaxLength,
    string? ConditionalFieldKey,
    string? ConditionalValue,
    Guid? ProjectCustomFieldId);

public record ExternalFormAssignmentRuleDto(
    string FieldKey,
    ExternalFormRuleOperator Operator,
    string? ExpectedValue,
    string? ResponsibleId,
    Guid? TeamId,
    Priority? Priority,
    Guid? StageId,
    int Position);

public record ExternalFormSummaryDto(
    Guid Id,
    string PublicSlug,
    string Title,
    string? Description,
    bool IsEnabled,
    bool IsDefault,
    string PublicPath);

public record ExternalFormDto(
    Guid Id,
    Guid ProjectId,
    Guid ExternalPortalId,
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
    string PublicPath);

public record ExternalRequestTriageEventDto(
    Guid Id,
    ExternalRequestTriageAction Action,
    string ActorName,
    string Description,
    string? DataJson,
    DateTimeOffset CreatedAt);

internal static class ExternalFormSerialization
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ExternalFormFieldDto> Fields(ExternalForm form)
        => Deserialize<ExternalFormFieldDto>(form.FieldsJson);

    public static IReadOnlyList<ExternalFormAssignmentRuleDto> Rules(ExternalForm form)
        => Deserialize<ExternalFormAssignmentRuleDto>(form.AssignmentRulesJson);

    public static IReadOnlyDictionary<string, string?> Values(ExternalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SubmittedValuesJson))
            return new Dictionary<string, string?>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(
                    request.SubmittedValuesJson, Options)
                ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static IReadOnlyList<T> Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

internal static class ExternalFormMapper
{
    public static ExternalFormSummaryDto Summary(ExternalForm form)
        => new(form.Id, form.PublicSlug, form.Title, form.Description, form.IsEnabled,
            form.IsDefault,
            $"/portal/{form.ExternalPortal.PublicSlug}?form={Uri.EscapeDataString(form.PublicSlug)}");

    public static ExternalFormDto Detail(ExternalForm form)
        => new(form.Id, form.ExternalPortal.ProjectId, form.ExternalPortalId, form.PublicSlug,
            form.Title, form.Description, form.Category, form.ConfirmationMessage,
            form.IsEnabled, form.IsDefault, form.DefaultPriority, form.InitialStageId,
            form.DefaultTeamId, form.DefaultResponsibleId, form.MaxFiles,
            form.MaxFileSizeBytes, form.AllowedExtensions, form.AllowedMimeTypes,
            form.MinimumCompletionSeconds,
            ExternalFormSerialization.Fields(form).OrderBy(x => x.Position).ToList(),
            ExternalFormSerialization.Rules(form).OrderBy(x => x.Position).ToList(),
            $"/portal/{form.ExternalPortal.PublicSlug}?form={Uri.EscapeDataString(form.PublicSlug)}");
}
