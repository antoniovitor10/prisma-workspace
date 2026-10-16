using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services.MethodGit;

public sealed class GitHubUnavailableException(string code, DateTimeOffset? retryAtUtc = null) : Exception("GitHub indisponível ou sem acesso completo. Avise o administrador.")
{
    public string Code { get; } = code;
    public DateTimeOffset? RetryAtUtc { get; } = retryAtUtc;
}

public sealed partial class GitHubProvider : IGitProvider
{
    public const string HttpClientName = "MethodGit.GitHub";
    private readonly IHttpClientFactory _clients;
    private readonly IMethodGitSecretStore _secrets;
    private readonly TimeProvider _clock;
    private readonly Uri _baseUri;
    private readonly ConcurrentDictionary<string, InstallationSession> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _notBefore = new();
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private sealed record InstallationSession(string Token, DateTimeOffset ExpiresAtUtc, string FullName);
    public string Provider => "github";

    public GitHubProvider(IHttpClientFactory clients, IMethodGitSecretStore secrets, IConfiguration configuration,
        IHostEnvironment environment, TimeProvider? clock = null)
    {
        _clients = clients; _secrets = secrets; _clock = clock ?? TimeProvider.System;
        var configured = configuration["MethodGit:GitHubApiBaseUrl"] ?? "https://api.github.com/";
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.EndsWith('/'))
            throw new InvalidOperationException("Base da API GitHub inválida.");
        var official = uri.Scheme == "https" && uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort && uri.AbsolutePath == "/";
        if (!official && (!(environment.IsDevelopment() || environment.IsEnvironment("Test")) || uri.Scheme is not ("https" or "http")))
            throw new InvalidOperationException("Base alternativa GitHub só é permitida em Development/Test pela configuração externa.");
        _baseUri = uri;
    }
    public bool VerifyWebhookSignature(ReadOnlySpan<byte> originalBody, string signature, string secret)
        => GitHubCryptography.VerifySignature(originalBody, signature, secret);
    public NormalizedGitEvent NormalizeWebhook(string eventName, ReadOnlySpan<byte> originalBody) => GitEventNormalizer.Normalize(eventName, originalBody);

    public async Task<GitInstallationValidation> ValidateInstallationAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await SessionAsync(repository, cancellationToken);
            if (!session.FullName.Equals(repository.RepositoryFullName, StringComparison.OrdinalIgnoreCase))
            {
                _sessions.TryRemove(SessionKey(repository), out _);
                session = await SessionAsync(repository, cancellationToken);
            }
            if (!session.FullName.Equals(repository.RepositoryFullName, StringComparison.OrdinalIgnoreCase))
                return new(false, "git_repository_name_mismatch", "Nome do repositório diverge do ID autorizado pela instalação.");
            return new(true, null, null);
        }
        catch (Exception exception) when (exception is GitHubUnavailableException or CryptographicException or FormatException or JsonException or MethodOperationException)
        { return new(false, "git_installation_unavailable", "Não foi possível validar a instalação e o repositório. Confira a configuração externa."); }
    }
    public async Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken cancellationToken = default)
    {
        if (number <= 0) throw new GitHubUnavailableException("invalid_pull_request");
        var session = await SessionAsync(repository, cancellationToken);
        using var json = await GetAsync(repository, session, $"repos/{session.FullName}/pulls/{number}", cancellationToken);
        var result = ParsePullRequest(json.RootElement, repository, session.FullName);
        if (result.Number != number) throw new GitHubUnavailableException("git_response_mismatch");
        return result;
    }
    public async Task<IReadOnlyList<GitReviewSnapshot>> GetReviewsAsync(GitRepositoryDescriptor repository, int number, CancellationToken cancellationToken = default)
    {
        var session = await SessionAsync(repository, cancellationToken);
        var result = new List<GitReviewSnapshot>();
        for (var page = 1; page <= 50; page++)
        {
            using var json = await GetAsync(repository, session, $"repos/{session.FullName}/pulls/{number}/reviews?per_page=100&page={page}", cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new GitHubUnavailableException("invalid_git_response");
            foreach (var row in json.RootElement.EnumerateArray())
            {
                var submitted = OptionalDate(row, "submitted_at");
                var state = RequiredString(row, "state", 32).ToLowerInvariant();
                if (state == "pending" && submitted is null) continue;
                if (submitted is null) throw new GitHubUnavailableException("invalid_git_review");
                result.Add(new(row.GetProperty("id").GetInt64(), row.GetProperty("user").GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture),
                    state, RequiredSha(row, "commit_id"), submitted.Value));
            }
            if (json.RootElement.GetArrayLength() < 100) return result;
        }
        throw new GitHubUnavailableException("git_review_pagination_incomplete");
    }
    public async Task<GitPullRequestReadSet> ListPullRequestsAsync(GitRepositoryDescriptor repository, string state, CancellationToken cancellationToken = default)
    {
        if (state is not ("open" or "closed" or "all")) throw new ArgumentException("Estado Git inválido.", nameof(state));
        var session = await SessionAsync(repository, cancellationToken);
        var result = new List<GitPullRequestSnapshot>();
        try
        {
            for (var page = 1; page <= 50; page++)
            {
                using var json = await GetAsync(repository, session, $"repos/{session.FullName}/pulls?state={state}&per_page=100&page={page}", cancellationToken);
                if (json.RootElement.ValueKind != JsonValueKind.Array) throw new GitHubUnavailableException("invalid_git_response");
                result.AddRange(json.RootElement.EnumerateArray().Select(row => ParsePullRequest(row, repository, session.FullName)));
                if (json.RootElement.GetArrayLength() < 100) return new(result, true);
            }
            return new(result, false, "git_pull_request_pagination_incomplete");
        }
        catch (GitHubUnavailableException exception) when (exception.Code != "git_rate_limited") { return new(result, false, exception.Code); }
    }
    private string InstallationKey(GitRepositoryDescriptor repository) => $"{repository.AppId}:{repository.InstallationId}";
    private void EnsureBudget(GitRepositoryDescriptor repository)
    {
        if (_notBefore.TryGetValue(InstallationKey(repository), out var until) && until > _clock.GetUtcNow())
            throw new GitHubUnavailableException("git_rate_limited", until);
    }
    private void RememberLimit(GitRepositoryDescriptor repository, GitHubUnavailableException exception)
    {
        if (exception.Code == "git_rate_limited") _notBefore[InstallationKey(repository)] = exception.RetryAtUtc ?? _clock.GetUtcNow().AddMinutes(1);
    }
    private string SessionKey(GitRepositoryDescriptor repository) => $"{repository.AppId}:{repository.InstallationId}:{repository.RepositoryId}:{repository.AppPrivateKeySecretRef}";
    private async Task<InstallationSession> SessionAsync(GitRepositoryDescriptor repository, CancellationToken cancellationToken)
    {
        if (repository.Provider != Provider || !repository.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || repository.AppId <= 0 || repository.InstallationId <= 0 || repository.RepositoryId <= 0)
            throw new GitHubUnavailableException("invalid_git_repository");
        EnsureBudget(repository);
        var key = SessionKey(repository);
        if (_sessions.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > _clock.GetUtcNow().AddMinutes(1)) return cached;
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_sessions.TryGetValue(key, out cached) && cached.ExpiresAtUtc > _clock.GetUtcNow().AddMinutes(1)) return cached;
            var privateKey = await _secrets.ResolveAsync(repository.AppPrivateKeySecretRef, cancellationToken);
            string jwt;
            try { jwt = GitHubCryptography.CreateAppJwt(repository.AppId, privateKey, _clock.GetUtcNow()); }
            catch (Exception exception) when (exception is CryptographicException or ArgumentException or FormatException)
            { throw new GitHubUnavailableException("git_app_key_invalid"); }
            using var tokenJson = await SendAsync(HttpMethod.Post, $"app/installations/{repository.InstallationId}/access_tokens", jwt,
                new { repository_ids = new[] { repository.RepositoryId }, permissions = new { pull_requests = "read", metadata = "read" } }, cancellationToken);
            var token = RequiredString(tokenJson.RootElement, "token", 16384);
            if (token.Any(char.IsWhiteSpace) || token.Any(char.IsControl)) throw new GitHubUnavailableException("invalid_installation_token");
            var expiry = OptionalDate(tokenJson.RootElement, "expires_at");
            if (expiry is null || expiry <= _clock.GetUtcNow()) throw new GitHubUnavailableException("invalid_installation_expiry");
            var permissions = tokenJson.RootElement.GetProperty("permissions");
            if (permissions.ValueKind != JsonValueKind.Object || !permissions.TryGetProperty("pull_requests", out var pulls) || pulls.GetString() != "read"
                || permissions.EnumerateObject().Any(p => p.Name is not ("pull_requests" or "metadata") || p.Value.GetString() != "read"))
                throw new GitHubUnavailableException("git_permission_mismatch");
            using var repos = await SendAsync(HttpMethod.Get, "installation/repositories?per_page=100&page=1", token, null, cancellationToken);
            var collection = repos.RootElement.GetProperty("repositories");
            if (collection.GetArrayLength() != 1 || repos.RootElement.GetProperty("total_count").GetInt32() != 1
                || collection[0].GetProperty("id").GetInt64() != repository.RepositoryId)
                throw new GitHubUnavailableException("git_repository_access_mismatch");
            var fullName = RequiredString(collection[0], "full_name", 300);
            if (!RepositoryNamePattern().IsMatch(fullName) || fullName.Split('/').Any(s => s is "." or ".."))
                throw new GitHubUnavailableException("invalid_git_repository_name");
            var session = new InstallationSession(token, expiry.Value, fullName);
            _sessions[key] = session;
            return session;
        }
        catch (GitHubUnavailableException exception) { RememberLimit(repository, exception); throw; }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new GitHubUnavailableException("invalid_git_response"); }
        finally { _tokenLock.Release(); }
    }
    private async Task<JsonDocument> GetAsync(GitRepositoryDescriptor repository, InstallationSession session, string path, CancellationToken cancellationToken)
    {
        EnsureBudget(repository);
        try { return await SendAsync(HttpMethod.Get, path, session.Token, null, cancellationToken); }
        catch (GitHubUnavailableException exception)
        {
            if (exception.Code is "git_access_unavailable" or "git_repository_moved") _sessions.TryRemove(SessionKey(repository), out _);
            RememberLimit(repository, exception); throw;
        }
    }
    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, string token, object? body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(method, new Uri(_baseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("Prisma-Method-Git/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await _clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var retry = response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is { } delta ? _clock.GetUtcNow().Add(delta) : null);
                if (response.StatusCode is HttpStatusCode.TooManyRequests || (response.StatusCode == HttpStatusCode.Forbidden
                    && (retry.HasValue || (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")))))
                {
                    if (retry is null && response.Headers.TryGetValues("X-RateLimit-Reset", out var reset)
                        && long.TryParse(reset.FirstOrDefault(), out var seconds) && seconds is >= 0 and <= 253402300799) retry = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    throw new GitHubUnavailableException("git_rate_limited", retry ?? _clock.GetUtcNow().AddMinutes(1));
                }
                throw new GitHubUnavailableException((int)response.StatusCode is >= 300 and < 400 ? "git_repository_moved"
                    : response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "git_access_unavailable" : "git_api_unavailable", retry);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) != 0)
            {
                if (memory.Length + read > 5 * 1024 * 1024) throw new GitHubUnavailableException("git_response_too_large");
                await memory.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            return JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 48 });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new GitHubUnavailableException("git_timeout"); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        { throw new GitHubUnavailableException("git_api_unavailable"); }
    }
    private static GitPullRequestSnapshot ParsePullRequest(JsonElement row, GitRepositoryDescriptor repository, string fullName)
    {
        try
        {
            var number = row.GetProperty("number").GetInt32();
            var head = row.GetProperty("head"); var @base = row.GetProperty("base");
            if (number <= 0 || @base.GetProperty("repo").GetProperty("id").GetInt64() != repository.RepositoryId)
                throw new GitHubUnavailableException("git_response_mismatch");
            var state = RequiredString(row, "state", 16);
            if (state is not ("open" or "closed")) throw new GitHubUnavailableException("invalid_git_state");
            var expectedUrl = $"https://github.com/{fullName}/pull/{number}";
            if (!RequiredString(row, "html_url", 1024).Equals(expectedUrl, StringComparison.OrdinalIgnoreCase))
                throw new GitHubUnavailableException("git_response_mismatch");
            var mergedAt = OptionalDate(row, "merged_at");
            var merged = row.TryGetProperty("merged", out var value) ? value.GetBoolean() : mergedAt.HasValue;
            return new(number, expectedUrl, RequiredString(row, "title", 1000, true), RequiredString(head, "ref", 255), RequiredString(@base, "ref", 255),
                RequiredSha(head, "sha"), OptionalSha(row, "merge_commit_sha"), state, row.GetProperty("draft").GetBoolean(), merged,
                OptionalDate(row, "updated_at") ?? throw new GitHubUnavailableException("invalid_git_timestamp"),
                OptionalDate(row, "created_at"), mergedAt, ClosedAtUtc: OptionalDate(row, "closed_at"));
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new GitHubUnavailableException("invalid_git_response"); }
    }
    private static string RequiredString(JsonElement parent, string name, int max, bool allowEmpty = false)
    {
        var value = parent.GetProperty(name).GetString();
        if (value is null || value.Length > max || (!allowEmpty && value.Length == 0) || value.Any(char.IsControl))
            throw new GitHubUnavailableException("invalid_git_response");
        return value;
    }
    private static string? OptionalString(JsonElement row, string name, int max) => !row.TryGetProperty(name, out var value)
        || value.ValueKind == JsonValueKind.Null ? null : RequiredString(row, name, max);
    private static string RequiredSha(JsonElement row, string name)
    {
        var value = RequiredString(row, name, 64);
        if (!ShaPattern().IsMatch(value)) throw new GitHubUnavailableException("invalid_git_sha");
        return value.ToLowerInvariant();
    }
    private static string? OptionalSha(JsonElement row, string name) => !row.TryGetProperty(name, out var value)
        || value.ValueKind == JsonValueKind.Null ? null : RequiredSha(row, name);
    private static DateTimeOffset? OptionalDate(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (!DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            throw new GitHubUnavailableException("invalid_git_timestamp");
        return date;
    }
    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)] private static partial Regex RepositoryNamePattern();
    [GeneratedRegex("^[a-fA-F0-9]{40}([a-fA-F0-9]{24})?$", RegexOptions.CultureInvariant)] private static partial Regex ShaPattern();
}
