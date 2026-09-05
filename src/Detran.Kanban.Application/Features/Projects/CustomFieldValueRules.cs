using System.Globalization;
using System.Text.Json;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Application.Features.Projects;

public static class CustomFieldValueRules
{
    public static void Validate(
        ProjectCustomFieldDefinition definition,
        string? value,
        Project project)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var valid = definition.Type switch
        {
            CustomFieldType.Text => value.Length <= 4_000,
            CustomFieldType.LongText => value.Length <= 20_000,
            CustomFieldType.Number => decimal.TryParse(
                value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            CustomFieldType.Percentage => decimal.TryParse(
                    value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentage)
                && percentage is >= 0 and <= 100,
            CustomFieldType.Date => DateOnly.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            CustomFieldType.DateTime => DateTimeOffset.TryParse(
                    value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out _)
                || DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out _),
            CustomFieldType.Boolean => bool.TryParse(value, out _),
            CustomFieldType.SingleSelect => IsAllowedOption(definition.OptionsJson, value, false),
            CustomFieldType.MultiSelect => IsAllowedOption(definition.OptionsJson, value, true),
            CustomFieldType.User => value == project.OwnerId
                || project.Members.Any(x => x.UserId == value),
            CustomFieldType.Team => Guid.TryParse(value, out var teamId)
                && project.Teams.Any(x => x.TeamId == teamId),
            CustomFieldType.Url => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            _ => false
        };
        DomainException.Garantir(valid, $"Valor inválido para o campo {definition.Name}.");
    }

    public static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(optionsJson ?? "[]")
                ?.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsAllowedOption(string? optionsJson, string value, bool multiple)
    {
        var options = ParseOptions(optionsJson);
        try
        {
            var values = multiple
                ? JsonSerializer.Deserialize<string[]>(value) ?? []
                : [value];
            return values.Length > 0
                && values.All(x => options.Contains(x, StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }
}
