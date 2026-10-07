using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Ai;

namespace Prisma.Workspace.Infrastructure.Services.Ai;

public sealed partial class AiService
{
    private sealed record ModelOption(string Id, string Name, decimal? InputPrice = null, decimal? OutputPrice = null);
    private static bool SameEndpoint(string? a, string? b) => string.Equals(a?.TrimEnd('/') ?? "", b?.TrimEnd('/') ?? "", StringComparison.Ordinal);
    private static void ValidateConnectionMethod(string type, string provider)
    {
        if (type == "CliSubscription" && provider is not ("OpenAI" or "Anthropic" or "Gemini")) throw new AiException(400, "Assinatura CLI disponível para OpenAI/Codex, Anthropic/Claude e Google/Gemini. Edite o provedor desta conexão.");
        if (type == "OAuth" && provider != "OpenRouter") throw new AiException(400, "Login disponível somente para OpenRouter.");
        if (provider == "Custom" && type != "OpenAiCompatible") throw new AiException(400, "Para endpoint personalizado, escolha conexão compatível com OpenAI.");
        if (type is not ("ApiKey" or "OpenAiCompatible" or "OAuth" or "CliSubscription") || provider is not ("OpenAI" or "Anthropic" or "Gemini" or "OpenRouter" or "Custom")) throw new AiException(400, "Provedor ou método de conexão inválido.");
    }
    private async Task<object> ModelsAsync(JsonElement a, CancellationToken ct)
    {
        var provider = S(a, "provider") ?? ""; var type = S(a, "type") ?? "";
        ValidateConnectionMethod(type, provider);
        if (type == "CliSubscription") return await BridgeAsync("models", provider, new { }, ct);
        var baseUrl = S(a, "baseUrl")?.Trim(); var secret = S(a, "secret");
        if (secret?.Length > 4096 || secret?.Any(char.IsControl) == true) throw new AiException(400, "Credencial inválida.");
        if (!string.IsNullOrEmpty(baseUrl) && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || baseUrl.Length > 2048)) throw new AiException(400, "Informe uma URL HTTP válida e sem credenciais.");
        if (type == "OAuth" && !string.IsNullOrEmpty(baseUrl)) throw new AiException(400, "Login OpenRouter usa o endereço oficial.");
        if (Guid.TryParse(S(a, "connectionId"), out var connectionId) && string.IsNullOrEmpty(secret))
        {
            var saved = await db.AiProviderConnections.SingleOrDefaultAsync(x => x.Id == connectionId, ct) ?? throw new AiException(404, "Conexão não encontrada.");
            if (saved.Provider == provider && saved.Type == type && SameEndpoint(saved.BaseUrl, baseUrl) && saved.EncryptedSecret is not null) secret = _protector.Unprotect(saved.EncryptedSecret);
        }
        if (provider != "OpenRouter" && provider != "Custom" && string.IsNullOrEmpty(secret)) return new { models = Array.Empty<ModelOption>(), state = "authenticationRequired", source = provider, message = "Informe a chave de API para carregar os modelos." };
        if (provider == "Custom" && string.IsNullOrEmpty(baseUrl)) return new { models = Array.Empty<ModelOption>(), state = "endpointRequired", source = provider, message = "Informe o endereço do endpoint para carregar os modelos." };
        var nativeGemini = provider == "Gemini" && string.IsNullOrEmpty(baseUrl);
        var nativeAnthropic = provider == "Anthropic" && string.IsNullOrEmpty(baseUrl);
        var rootUrl = baseUrl ?? provider switch { "OpenAI" => "https://api.openai.com/v1", "Anthropic" => "https://api.anthropic.com/v1", "Gemini" => "https://generativelanguage.googleapis.com/v1beta", "OpenRouter" => "https://openrouter.ai/api/v1", _ => throw new AiException(400, "URL base obrigatória.") };
        var models = new List<ModelOption>(); var url = rootUrl.TrimEnd('/') + "/models";
        if (nativeGemini) url += "?pageSize=1000";
        if (nativeAnthropic) url += "?limit=1000";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var client = clients.CreateClient("prisma-ai");
            for (var page = 0; page < 3 && models.Count < 2000; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(secret))
                {
                    if (nativeGemini) request.Headers.Add("x-goog-api-key", secret);
                    else if (nativeAnthropic) request.Headers.Add("x-api-key", secret);
                    else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                }
                if (nativeAnthropic) request.Headers.Add("anthropic-version", "2023-06-01");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode) return new { models = Array.Empty<ModelOption>(), state = "unavailable", source = provider, message = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ? "O provedor recusou a credencial. Confira a chave ou autentique novamente." : "O catálogo não está disponível neste endereço. Confira a conexão e tente novamente.", manualAllowed = provider == "Custom" || !string.IsNullOrEmpty(baseUrl) };
                using var data = await ReadLimitedJsonAsync(response, 8_000_000, timeout.Token);
                var rows = data.RootElement.TryGetProperty(nativeGemini ? "models" : "data", out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray() : JsonSerializer.SerializeToElement(Array.Empty<object>()).EnumerateArray();
                foreach (var row in rows)
                {
                    var id = S(row, nativeGemini ? "name" : "id") ?? "";
                    if (nativeGemini)
                    {
                        if (!row.TryGetProperty("supportedGenerationMethods", out var methods) || !methods.EnumerateArray().Any(x => x.GetString() == "generateContent")) continue;
                        id = id.Replace("models/", "", StringComparison.Ordinal);
                    }
                    if (id.Length is < 1 or > 200 || id.Any(char.IsControl)) continue;
                    if (provider == "OpenAI" && string.IsNullOrEmpty(baseUrl) && new[] { "embedding", "moderation", "whisper", "tts", "dall-e", "image", "realtime", "transcribe", "audio" }.Any(part => id.Contains(part, StringComparison.OrdinalIgnoreCase))) continue;
                    if (provider == "OpenRouter" && row.TryGetProperty("architecture", out var architecture) && architecture.TryGetProperty("output_modalities", out var modalities) && !modalities.EnumerateArray().Any(x => x.GetString() == "text")) continue;
                    var name = S(row, nativeGemini ? "displayName" : nativeAnthropic ? "display_name" : "name") ?? id;
                    decimal? inputPrice = null, outputPrice = null;
                    if (provider == "OpenRouter" && row.TryGetProperty("pricing", out var pricing))
                    {
                        inputPrice = TokenPrice(pricing, "prompt"); outputPrice = TokenPrice(pricing, "completion");
                    }
                    models.Add(new(id, name.Length > 200 ? name[..200] : name, inputPrice, outputPrice));
                }
                if (nativeGemini && S(data.RootElement, "nextPageToken") is { Length: > 0 } next) url = rootUrl.TrimEnd('/') + "/models?pageSize=1000&pageToken=" + Uri.EscapeDataString(next);
                else if (nativeAnthropic && B(data.RootElement, "has_more") && S(data.RootElement, "last_id") is { Length: > 0 } last) url = rootUrl.TrimEnd('/') + "/models?limit=1000&after_id=" + Uri.EscapeDataString(last);
                else break;
            }
            var result = models.DistinctBy(x => x.Id).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(2000).ToArray();
            return new { models = result, state = result.Length == 0 ? "empty" : "ready", source = provider, message = result.Length == 0 ? "Nenhum modelo de conversa foi informado pelo endpoint." : "Escolha um modelo; o teste verifica o acesso da sua conta.", manualAllowed = provider == "Custom" || !string.IsNullOrEmpty(baseUrl) };
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidDataException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return new { models = Array.Empty<ModelOption>(), state = "unavailable", source = provider, message = "Não foi possível carregar os modelos. Confira o endereço e tente novamente.", manualAllowed = provider == "Custom" || !string.IsNullOrEmpty(baseUrl) };
        }
    }
    private static decimal? TokenPrice(JsonElement a, string key) => decimal.TryParse(S(a, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0 and < 1_000_000 ? value * 1_000_000 : null;
    private async Task<object> CliAsync(string operation, string userId, JsonElement a, CancellationToken ct)
    {
        var provider = S(a, "provider") ?? "";
        ValidateConnectionMethod("CliSubscription", provider);
        if (operation == "login" && !B(a, "acceptedRisk")) throw new AiException(400, "Leia e aceite o risco de bloqueio ou perda da conta antes de autenticar por CLI.");
        var sessionId = S(a, "sessionId"); var code = S(a, "code");
        if (operation is "progress" or "complete" or "cancel" && (sessionId is null || !Regex.IsMatch(sessionId, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))) throw new AiException(400, "Sessão de autenticação inválida.");
        if (operation == "complete" && (code is null || code.Length is < 1 or > 4096 || code.Any(char.IsControl))) throw new AiException(400, "Informe o código fornecido pelo provedor.");
        var result = await BridgeAsync(operation, provider, new { owner = userId, sessionId, code, acceptedRisk = B(a, "acceptedRisk") }, ct);
        if (operation == "login" && result is JsonElement json && S(json, "sessionId") is { Length: > 0 })
        {
            // Trocar a conta da ponte exige testar novamente todas as conexões desse adaptador.
            foreach (var connection in await db.AiProviderConnections.Where(x => x.Type == "CliSubscription" && x.Provider == provider).ToListAsync(ct))
            { ChatCatalogs.TryRemove(connection.Id, out _); connection.IsActive = false; connection.TestSucceeded = false; connection.TestMessage = "Login CLI iniciado. Conclua a autenticação e teste novamente."; }
            CancelAll(); await db.SaveChangesAsync(ct);
        }
        return result;
    }
    private async Task<object> BridgeAsync(string operation, string provider, object payload, CancellationToken ct)
    {
        var adapter = provider switch { "OpenAI" => "codex", "Anthropic" => "claude", "Gemini" => "gemini", _ => throw new AiException(400, "CLI inválida.") };
        var token = config["Ai:BridgeToken"];
        if (string.IsNullOrEmpty(token)) return new { available = false, authenticated = false, state = "unavailable", models = Array.Empty<ModelOption>(), message = "A conexão CLI não está instalada. Use chave de API ou configure a ponte de autenticação." };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(25));
            using var client = clients.CreateClient("prisma-ai");
            using var request = new HttpRequestMessage(HttpMethod.Post, (config["Ai:BridgeUrl"] ?? "http://prisma-ai-bridge:8080/v1").TrimEnd('/') + "/cli/" + adapter + "/" + operation);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // A ponte Python exige tamanho conhecido; JsonContent usa envio chunked.
            request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new AiException(response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden ? 404 : response.StatusCode == System.Net.HttpStatusCode.Conflict ? 409 : 502, response.StatusCode == System.Net.HttpStatusCode.Conflict ? "Já existe um login em andamento para esta CLI. Aguarde ou cancele sua tentativa." : "Não foi possível consultar esta sessão CLI. Confira a instalação ou inicie novamente.");
            using var data = await ReadLimitedJsonAsync(response, 256_000, timeout.Token); return data.RootElement.Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidDataException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { return new { available = false, authenticated = false, state = "unavailable", models = Array.Empty<ModelOption>(), message = "A ponte CLI está indisponível. Use chave de API ou tente novamente após verificar a instalação." }; }
    }
    private static async Task<JsonDocument> ReadLimitedJsonAsync(HttpResponseMessage response, int limit, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var bytes = new MemoryStream(); var buffer = new byte[8192];
        while (await stream.ReadAsync(buffer, ct) is var read && read > 0) { if (bytes.Length + read > limit) throw new InvalidDataException(); await bytes.WriteAsync(buffer.AsMemory(0, read), ct); }
        bytes.Position = 0; return await JsonDocument.ParseAsync(bytes, cancellationToken: ct);
    }
}
