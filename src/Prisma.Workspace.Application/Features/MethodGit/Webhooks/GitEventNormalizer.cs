using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Features.MethodGit.Webhooks;

/// <summary>Descarta descrição/corpo de review; autor Git observado é metadado separado da autoridade.</summary>
public static partial class GitEventNormalizer
{
    public static NormalizedGitEvent Normalize(string eventName, ReadOnlySpan<byte> originalBody)
    {
        try
        {
            using var document = JsonDocument.Parse(originalBody.ToArray(), new JsonDocumentOptions { MaxDepth = 48 });
            var root = document.RootElement;
            var repositoryId = PositiveId(root.GetProperty("repository"), "id");
            long? installationId = root.TryGetProperty("installation", out var installation)
                && installation.ValueKind != JsonValueKind.Null ? PositiveId(installation, "id") : null;
            if (eventName == "create")
            {
                var branch = Text(root, "ref", 255);
                var refType = Text(root, "ref_type", 32);
                return AttachActor(new("github", repositoryId, installationId, eventName, refType == "branch" ? "branch" : "unsupported",
                    DateTimeOffset.UtcNow, branch, branch, null, null, null, null, null, null, null, null), root);
            }
            if (eventName is not ("pull_request" or "pull_request_review"))
                return AttachActor(new("github", repositoryId, installationId, eventName, null, DateTimeOffset.UtcNow,
                    "ignored", null, null, null, null, null, null, null, null, null), root);

            var pr = root.GetProperty("pull_request");
            var number = PositiveId(pr, "number");
            if (number > int.MaxValue) throw Invalid();
            var head = pr.GetProperty("head");
            var @base = pr.GetProperty("base");
            var headSha = Sha(head, "sha");
            var updated = Timestamp(pr, "updated_at");
            var action = Text(root, "action", 64);
            if (!ActionPattern().IsMatch(action)) throw Invalid();
            var state = Text(pr, "state", 16);
            if (state is not ("open" or "closed")) throw Invalid();
            var url = Text(pr, "html_url", 1024);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != "https"
                || !string.Equals(parsed.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || parsed.UserInfo.Length != 0 || parsed.Query.Length != 0 || parsed.Fragment.Length != 0)
                throw Invalid();
            long? reviewId = null;
            string? reviewState = null, reviewCommit = null;
            var occurred = updated;
            if (eventName == "pull_request_review")
            {
                var review = root.GetProperty("review");
                reviewId = PositiveId(review, "id");
                reviewState = Text(review, "state", 32).ToLowerInvariant();
                if (reviewState is not ("approved" or "changes_requested" or "commented" or "dismissed" or "pending")) throw Invalid();
                reviewCommit = Sha(review, "commit_id");
                occurred = Timestamp(review, "submitted_at");
            }
            else if (action == "closed" && pr.TryGetProperty("closed_at", out var closed) && closed.ValueKind == JsonValueKind.String)
                occurred = Timestamp(pr, "closed_at");
            var revision = $"{action}:{occurred:O}:{headSha}";
            if (reviewId.HasValue) revision = $"review:{reviewId}:{reviewState}:{occurred:O}:{reviewCommit}";
            revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision))).ToLowerInvariant();
            return AttachActor(new("github", repositoryId, installationId, eventName, action, occurred, revision, null,
                (int)number, url, Text(pr, "title", 1000, allowEmpty: true), Text(head, "ref", 255),
                Text(@base, "ref", 255), headSha, OptionalSha(pr, "merge_commit_sha"), state,
                Boolean(pr, "draft"), Boolean(pr, "merged"), reviewId, reviewState, reviewCommit), root);
        }
        catch (MethodOperationException) { throw; }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw Invalid(); }
    }
    private static NormalizedGitEvent AttachActor(NormalizedGitEvent value, JsonElement root)
    {
        if (!root.TryGetProperty("sender", out var sender) || sender.ValueKind == JsonValueKind.Null) return value;
        return value with { ObservedGitActorId = PositiveId(sender, "id").ToString(CultureInfo.InvariantCulture),
            ObservedGitActorName = Text(sender, "login", 200) };
    }
    private static MethodOperationException Invalid() => new(400, "invalid_git_payload", "Payload GitHub inválido ou incompleto.");
    private static long PositiveId(JsonElement parent, string name)
    {
        if (!parent.GetProperty(name).TryGetInt64(out var value) || value <= 0) throw Invalid();
        return value;
    }
    private static string Text(JsonElement parent, string name, int length, bool allowEmpty = false)
    {
        var value = parent.GetProperty(name).GetString();
        if (value is null || value.Length > length || (!allowEmpty && value.Length == 0) || value.Any(char.IsControl)) throw Invalid();
        return value;
    }
    private static DateTimeOffset Timestamp(JsonElement parent, string name)
    {
        if (!DateTimeOffset.TryParse(parent.GetProperty(name).GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)) throw Invalid();
        return value;
    }
    private static bool Boolean(JsonElement parent, string name) => parent.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : false;
    private static string Sha(JsonElement parent, string name)
    {
        var value = Text(parent, name, 64);
        if (!ShaPattern().IsMatch(value)) throw Invalid();
        return value.ToLowerInvariant();
    }
    private static string? OptionalSha(JsonElement parent, string name) => !parent.TryGetProperty(name, out var value)
        || value.ValueKind == JsonValueKind.Null ? null : Sha(parent, name);
    [GeneratedRegex("^[a-fA-F0-9]{40}([a-fA-F0-9]{24})?$", RegexOptions.CultureInvariant)] private static partial Regex ShaPattern();
    [GeneratedRegex("^[a-z_]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex ActionPattern();
}
