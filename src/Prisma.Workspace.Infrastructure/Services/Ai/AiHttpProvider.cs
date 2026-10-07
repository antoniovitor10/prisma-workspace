using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.Ai;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services.Ai;

public sealed class AiHttpProvider(IHttpClientFactory clients, IAiRedactionService? redaction = null, IConfiguration? configuration = null) : IAiChatProvider, IAiProviderFactory
{
    internal static readonly string[] ToolNames = ["search_workspace", "list_work_items", "get_work_item", "get_project_overview", "get_recent_activity", "get_my_work"];
    public IAiChatProvider Create(AiProviderConnection connection) => this;
    private static readonly object Schema = new { type = "object", properties = new {
        query = new { type = "string" }, id = new { type = "string" }, projectId = new { type = "string" },
        stageId = new { type = "string" }, responsibleId = new { type = "string" }, priority = new { type = "integer" },
        overdue = new { type = "boolean" }, dueFrom = new { type = "string" }, dueTo = new { type = "string" },
        updatedSince = new { type = "string" }, since = new { type = "string" } } };

    public async Task<AiProviderResult> CompleteAsync(AiProviderConnection c, string? secret,
        IReadOnlyList<AiProviderMessage> messages, bool nativeTools, int maxTokens, Func<string, Task>? delta, CancellationToken ct)
    {
        var anthropic = c.Provider == "Anthropic" && c.BaseUrl is null && c.Type != "OpenAiCompatible" && c.Type != "CliSubscription";
        var baseUrl = c.Type == "CliSubscription" ? configuration?["Ai:BridgeUrl"] ?? "http://prisma-ai-bridge:8080/v1" : c.BaseUrl ?? c.Provider switch {
            "OpenAI" => "https://api.openai.com/v1", "Anthropic" => "https://api.anthropic.com/v1",
            "Gemini" => "https://generativelanguage.googleapis.com/v1beta/openai", "OpenRouter" => "https://openrouter.ai/api/v1",
            _ => throw new AiException(400, "URL base obrigatória.") };
        var payload = new Dictionary<string, object?> { ["model"] = c.Model, ["stream"] = true, ["max_tokens"] = maxTokens };
        if (c.Type == "CliSubscription") payload["adapter"] = c.Provider switch { "OpenAI" => "codex", "Anthropic" => "claude", "Gemini" => "gemini", _ => throw new AiException(400, "Escolha Codex, Claude ou Gemini para assinatura CLI.") };
        if (anthropic)
        {
            payload["system"] = string.Join("\n", messages.Where(x => x.Role == "system").Select(x => x.Content));
            payload["messages"] = messages.Where(x => x.Role != "system").Select(x => new {
                role = x.Role == "tool" ? "user" : x.Role,
                content = AnthropicContent(x) }).ToArray();
            if (nativeTools) payload["tools"] = ToolNames.Select(name => new { name, description = ToolDescription(name), input_schema = Schema }).ToArray();
        }
        else
        {
            payload["messages"] = messages.Select(x => {
                var m = new Dictionary<string, object?> { ["role"] = x.Role, ["content"] = x.Content };
                if (x.ToolCallId is not null) m["tool_call_id"] = x.ToolCallId;
                if (x.Calls?.Count > 0) m["tool_calls"] = x.Calls.Select(call => new { call.Id, type = "function", function = new { name = call.Name, arguments = call.Arguments } });
                return m;
            }).ToArray();
            payload["stream_options"] = new { include_usage = true };
            if (nativeTools) payload["tools"] = ToolNames.Select(name => new { type = "function", function = new { name, description = ToolDescription(name), parameters = Schema } }).ToArray();
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + (anthropic ? "/messages" : "/chat/completions"));
        request.Content = c.Type == "CliSubscription"
            ? new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
            : JsonContent.Create(payload);
        if (!string.IsNullOrEmpty(secret))
        {
            if (anthropic) request.Headers.Add("x-api-key", secret);
            else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }
        if (anthropic) request.Headers.Add("anthropic-version", "2023-06-01");
        using var client = clients.CreateClient("prisma-ai");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = "";
            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Length <= 64000)
            {
                try { using var error = JsonDocument.Parse(body); if (error.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m)) detail = m.GetString() ?? ""; }
                catch (JsonException) { }
            }
            if (!string.IsNullOrEmpty(secret)) detail = detail.Replace(secret, "[oculto]", StringComparison.Ordinal);
            detail = (redaction ?? new AiRedactionService()).Redact(detail);
            if (detail.Length > 500) detail = detail[..500];
            throw new AiException(502, $"O provedor recusou a chamada (HTTP {(int)response.StatusCode}). {detail}".Trim());
        }
        var text = new StringBuilder();
        var calls = new Dictionary<int, (string Id, StringBuilder Name, StringBuilder Arguments)>();
        long input = 0, output = 0;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;
            if (data.Length > 1_000_000) throw new AiException(502, "Resposta do provedor excedeu o limite.");
            using var doc = JsonDocument.Parse(data); var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) throw new AiException(502, "O provedor interrompeu a resposta.");
            if (anthropic)
            {
                var type = root.GetProperty("type").GetString();
                if (type == "message_start" && root.TryGetProperty("message", out var msg) && msg.TryGetProperty("usage", out var u)) input = Number(u, "input_tokens");
                if (type == "message_delta" && root.TryGetProperty("usage", out var usage)) output = Number(usage, "output_tokens");
                if (type == "content_block_start" && root.GetProperty("content_block").GetProperty("type").GetString() == "tool_use")
                {
                    var b = root.GetProperty("content_block");
                    calls[root.GetProperty("index").GetInt32()] = (b.GetProperty("id").GetString()!, new(b.GetProperty("name").GetString()), new());
                }
                if (type == "content_block_delta")
                {
                    var d = root.GetProperty("delta");
                    if (d.TryGetProperty("text", out var t)) await Append(t.GetString()!);
                    if (d.TryGetProperty("partial_json", out var j) && calls.TryGetValue(root.GetProperty("index").GetInt32(), out var call)) call.Arguments.Append(j.GetString());
                }
            }
            else
            {
                if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object) { input = Number(u, "prompt_tokens"); output = Number(u, "completion_tokens"); }
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) continue;
                var d = choices[0].GetProperty("delta");
                if (d.TryGetProperty("content", out var t) && t.ValueKind == JsonValueKind.String) await Append(t.GetString()!);
                if (d.TryGetProperty("tool_calls", out var toolCalls)) foreach (var part in toolCalls.EnumerateArray())
                {
                    var index = part.GetProperty("index").GetInt32();
                    if (!calls.TryGetValue(index, out var call)) call = (part.TryGetProperty("id", out var id) ? id.GetString()! : Guid.NewGuid().ToString("N"), new(), new());
                    if (part.TryGetProperty("function", out var f))
                    {
                        if (f.TryGetProperty("name", out var n)) call.Name.Append(n.GetString());
                        if (f.TryGetProperty("arguments", out var a)) call.Arguments.Append(a.GetString());
                    }
                    calls[index] = call;
                }
            }
            if (calls.Count > 8 || text.Length > 64000 || calls.Values.Sum(x => x.Arguments.Length) > 64000) throw new AiException(502, "Resposta do provedor excedeu o limite.");
        }
        if (text.Length == 0 && calls.Count == 0) throw new AiException(502, "O provedor não retornou conteúdo ou ferramentas.");
        return new(text.ToString(), calls.Values.Select(x => new AiToolCall(x.Id, x.Name.ToString(), x.Arguments.ToString())).ToArray(), input, output);
        async Task Append(string value) { text.Append(value); if (delta is not null) await delta(value); }
    }
    private static long Number(JsonElement j, string key) => j.TryGetProperty(key, out var v) && v.TryGetInt64(out var n) ? Math.Max(0, n) : 0;
    private static object[] AnthropicContent(AiProviderMessage m)
    {
        if (m.Role == "tool") return [new { type = "tool_result", tool_use_id = m.ToolCallId, content = m.Content }];
        var blocks = new List<object>();
        if (m.Content.Length > 0) blocks.Add(new { type = "text", text = m.Content });
        if (m.Calls is not null) blocks.AddRange(m.Calls.Select(x => (object)new { type = "tool_use", id = x.Id, name = x.Name, input = JsonSerializer.Deserialize<JsonElement>(x.Arguments) }));
        return blocks.ToArray();
    }
    private static string ToolDescription(string name) => name switch {
        "get_project_overview" => "Contagens por coluna, atrasadas, sem responsável e sprint ativa. Informe id do projeto.",
        "get_work_item" => "Campos, checklist, subtarefas, comentários e metadados dos anexos da tarefa. Informe id.",
        "list_work_items" => "Consultar tarefas por projectId, stageId, responsibleId, priority, overdue, dueFrom, dueTo, updatedSince.",
        "get_recent_activity" => "Eventos recentes do projeto atual desde since (data ISO).",
        "get_my_work" => "Tarefas atribuídas à pessoa que pergunta.", _ => "Busca textual de projetos e tarefas visíveis. Informe query." };
}
