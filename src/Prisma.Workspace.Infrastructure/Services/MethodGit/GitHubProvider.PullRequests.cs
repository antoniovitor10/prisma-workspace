using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

/// <summary>
/// HU-0007 · T-0027 (D3): abrir PR pela GitHub App. A sessão de leitura continua como está (pede e exige só leitura);
/// a criação usa um token de escrita separado, pedido no clique para um único repositório com exatamente
/// <c>pull_requests: write</c> + <c>metadata: read</c>, nunca guardado em cache nem reaproveitado por leitura.
/// O token nunca vai para log, exceção ou resposta: as recusas saem só como códigos fixos.
/// </summary>
public sealed partial class GitHubProvider
{
    private const int ErrorBodyLimit = 64 * 1024;

    public async Task<GitPullRequestSnapshot?> FindOpenPullRequestByHeadAsync(GitRepositoryDescriptor repository, string branch,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await SessionAsync(repository, cancellationToken);
            var owner = session.FullName.Split('/')[0];
            using var json = await GetAsync(repository, session,
                $"repos/{session.FullName}/pulls?state=open&head={Uri.EscapeDataString($"{owner}:{branch}")}&per_page=100", cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new GitHubUnavailableException("invalid_git_response");
            foreach (var row in json.RootElement.EnumerateArray())
            {
                var pr = ParsePullRequest(row, repository, session.FullName);
                // Só a branch do próprio repositório: um fork com o mesmo nome de branch não é o PR da task.
                if (pr.State == "open" && pr.HeadBranch == branch && pr.HeadRepositoryId == repository.RepositoryId) return pr;
            }
            return null;
        }
        catch (GitHubUnavailableException exception) { throw Rejected(exception); }
    }

    public async Task<GitPullRequestSnapshot> CreatePullRequestAsync(GitRepositoryDescriptor repository, GitPullRequestCreation pullRequest,
        CancellationToken cancellationToken = default)
    {
        // O nome do repositório vem da sessão de leitura (conferido contra o ID estável da instalação).
        InstallationSession session;
        try { session = await SessionAsync(repository, cancellationToken); EnsureBudget(repository); }
        catch (GitHubUnavailableException exception) { throw Rejected(exception); }
        var token = await WriteTokenAsync(repository, cancellationToken);
        try
        {
            using var json = await SendPullRequestWriteAsync(repository, $"repos/{session.FullName}/pulls", token,
                new { title = pullRequest.Title, head = pullRequest.Head, @base = pullRequest.Base, body = pullRequest.Body, draft = pullRequest.Draft },
                tokenRequest: false, cancellationToken);
            var created = ParsePullRequest(json.RootElement, repository, session.FullName);
            if (created.HeadBranch != pullRequest.Head) throw new GitHubUnavailableException("git_response_mismatch");
            return created;
        }
        catch (GitHubUnavailableException exception) { throw Rejected(exception); }
    }

    /// <summary>Token de instalação de escrita, só para este repositório e só PRs; conferido na resposta e descartado após o uso.</summary>
    private async Task<string> WriteTokenAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken)
    {
        string jwt;
        try
        {
            var privateKey = await _secrets.ResolveAsync(repository.AppPrivateKeySecretRef, cancellationToken);
            jwt = GitHubCryptography.CreateAppJwt(repository.AppId, privateKey, _clock.GetUtcNow());
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or FormatException or InvalidOperationException)
        { throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.PermissionMissing); }
        try
        {
            using var tokenJson = await SendPullRequestWriteAsync(repository, $"app/installations/{repository.InstallationId}/access_tokens", jwt,
                new { repository_ids = new[] { repository.RepositoryId }, permissions = new { pull_requests = "write", metadata = "read" } },
                tokenRequest: true, cancellationToken);
            var root = tokenJson.RootElement;
            var token = RequiredString(root, "token", 16384);
            if (token.Any(char.IsWhiteSpace) || token.Any(char.IsControl)) throw new GitHubUnavailableException("invalid_installation_token");
            var expiry = OptionalDate(root, "expires_at");
            if (expiry is null || expiry <= _clock.GetUtcNow()) throw new GitHubUnavailableException("invalid_installation_expiry");
            if (!HasExactWritePermissions(root)) throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.PermissionMissing);
            return token;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Unavailable); }
        catch (GitHubUnavailableException exception) { throw Rejected(exception); }
    }

    // Exatamente pull_requests: write e metadata: read; qualquer permissão a mais ou a menos recusa o token.
    private static bool HasExactWritePermissions(JsonElement root)
    {
        if (!root.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Object) return false;
        var granted = permissions.EnumerateObject().ToArray();
        return granted.Length == 2
            && granted.Any(p => p.Name == "pull_requests" && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "write")
            && granted.Any(p => p.Name == "metadata" && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "read");
    }

    /// <summary>POST de escrita (token de instalação ou criação do PR) com a classificação de erros da D5.</summary>
    private async Task<JsonDocument> SendPullRequestWriteAsync(GitRepositoryDescriptor repository, string path, string bearer, object body,
        bool tokenRequest, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("Prisma-Method-Git/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        request.Content = JsonContent.Create(body);
        try
        {
            using var response = await _clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (RateLimitRetry(response) is { } retry)
                {
                    _notBefore[InstallationKey(repository)] = retry;
                    throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.RateLimited, retry);
                }
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                    || (tokenRequest && response.StatusCode == HttpStatusCode.UnprocessableEntity))
                    // No token: permissão nova ainda não aceita na instalação (422) ou App sem acesso; na criação: App sem escrita.
                    throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.PermissionMissing);
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                    throw new GitPullRequestRejectedException(ClassifyUnprocessable(await ReadLimitedAsync(response, ErrorBodyLimit, timeout.Token)));
                throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Unavailable);
            }
            var content = await ReadLimitedAsync(response, 5 * 1024 * 1024, timeout.Token)
                ?? throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Unavailable);
            return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 48 });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Timeout); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        { throw new GitPullRequestRejectedException(GitPullRequestErrorCodes.Unavailable); }
    }

    private DateTimeOffset? RateLimitRetry(HttpResponseMessage response)
    {
        var retry = response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is { } delta ? _clock.GetUtcNow().Add(delta) : null);
        var exhausted = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0");
        if (response.StatusCode != HttpStatusCode.TooManyRequests && !(response.StatusCode == HttpStatusCode.Forbidden && (retry.HasValue || exhausted)))
            return null;
        if (retry is null && response.Headers.TryGetValues("X-RateLimit-Reset", out var reset)
            && long.TryParse(reset.FirstOrDefault(), out var seconds) && seconds is >= 0 and <= 253402300799) retry = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return retry ?? _clock.GetUtcNow().AddMinutes(1);
    }

    /// <summary>Lê no máximo <paramref name="limit"/> bytes; acima disso devolve null (resposta grande demais para confiar).</summary>
    private static async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + read > limit) return null;
            await memory.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return memory.ToArray();
    }

    /// <summary>422 do POST /pulls: classifica por errors[].field/code e pelo prefixo da mensagem, sem repassar o texto.</summary>
    internal static string ClassifyUnprocessable(byte[]? content)
    {
        if (content is null) return GitPullRequestErrorCodes.Invalid;
        try
        {
            using var json = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 16 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return GitPullRequestErrorCodes.Invalid;
            var messages = new List<string>();
            var fields = new List<(string? Field, string? Code)>();
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) messages.Add(message.GetString() ?? string.Empty);
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.String) { messages.Add(error.GetString() ?? string.Empty); continue; }
                    if (error.ValueKind != JsonValueKind.Object) continue;
                    if (error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String) messages.Add(text.GetString() ?? string.Empty);
                    fields.Add((Text(error, "field"), Text(error, "code")));
                }
            if (messages.Any(m => m.StartsWith("A pull request already exists", StringComparison.OrdinalIgnoreCase))) return GitPullRequestErrorCodes.AlreadyExists;
            if (messages.Any(m => m.StartsWith("No commits between", StringComparison.OrdinalIgnoreCase))) return GitPullRequestErrorCodes.NothingToCompare;
            if (fields.Any(f => f.Field == "head" && f.Code is "invalid" or "missing" or "missing_field")) return GitPullRequestErrorCodes.HeadMissing;
            if (messages.Any(m => m.Contains("draft", StringComparison.OrdinalIgnoreCase))) return GitPullRequestErrorCodes.DraftUnsupported;
            return GitPullRequestErrorCodes.Invalid;
        }
        catch (JsonException) { return GitPullRequestErrorCodes.Invalid; }
        static string? Text(JsonElement parent, string name)
            => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // Erros da sessão de leitura e da resposta, já como códigos do contrato do botão (D5).
    private static GitPullRequestRejectedException Rejected(GitHubUnavailableException exception) => exception.Code switch
    {
        "git_rate_limited" => new(GitPullRequestErrorCodes.RateLimited, exception.RetryAtUtc),
        "git_timeout" => new(GitPullRequestErrorCodes.Timeout),
        "git_access_unavailable" or "git_permission_mismatch" or "git_repository_access_mismatch" or "git_app_key_invalid"
            or "invalid_git_repository" => new(GitPullRequestErrorCodes.PermissionMissing),
        _ => new(GitPullRequestErrorCodes.Unavailable),
    };
}
