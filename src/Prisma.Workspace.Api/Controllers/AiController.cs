using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.Ai;

namespace Prisma.Workspace.Api.Controllers;

[ApiController, Authorize(Policy = "InternalUser"), Route("api/ai")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiController(IMediator mediator) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Ok(await mediator.Send(new AiQuery("status", UserId), ct));
    [HttpGet("conversations")] public async Task<IActionResult> List(CancellationToken ct) => Ok(await mediator.Send(new AiQuery("conversations", UserId), ct));
    [HttpPost("conversations")] public async Task<IActionResult> Create(CancellationToken ct) => Ok(await mediator.Send(new AiCommand("createConversation", UserId), ct));
    [HttpGet("conversations/{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new AiQuery("conversation", UserId, id), ct));
    [HttpPatch("conversations/{id:guid}")] public async Task<IActionResult> Rename(Guid id, JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("renameConversation", UserId, id, input), ct));
    [HttpDelete("conversations/{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new AiCommand("deleteConversation", UserId, id), ct); return NoContent(); }
    [HttpDelete("conversations/{id:guid}/messages/current")] public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await mediator.Send(new AiCommand("cancelMessage", UserId, id), ct); return NoContent(); }
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task Send(Guid id, JsonElement input, CancellationToken ct)
    {
        async Task Emit(string name, object data)
        {
            if (ct.IsCancellationRequested) return;
            if (!Response.HasStarted) { Response.ContentType = "text/event-stream"; Response.Headers.CacheControl = "no-store"; Response.Headers["X-Accel-Buffering"] = "no"; }
            await Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web))}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        try { await mediator.Send(new AiCommand("message", UserId, id, input, Emit), ct); }
        catch (AiException e)
        {
            if (!Response.HasStarted) { Response.StatusCode = e.Status; await Response.WriteAsJsonAsync(new { detail = e.Message, e.ResetsAt }, ct); }
            else await Emit("error", new { detail = e.Message, status = e.Status, e.ResetsAt });
        }
        catch (OperationCanceledException) { if (!ct.IsCancellationRequested) await Emit("done", new { cancelled = true }); }
        catch (JsonException) { await Emit("error", new { detail = "O provedor retornou argumentos inválidos.", status = 502 }); }
    }
}

[ApiController, Authorize(Policy = "InternalUser"), Route("api/admin/ai")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiAdminController(IMediator mediator) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("connections")] public async Task<IActionResult> List(CancellationToken ct) => Ok(await mediator.Send(new AiQuery("admin.connections", UserId), ct));
    [HttpPost("models")] public async Task<IActionResult> Models(JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiQuery("admin.models", UserId, Input: input), ct));
    [HttpGet("cli/{provider}/status")] public async Task<IActionResult> CliStatus(string provider, CancellationToken ct) => Ok(await mediator.Send(new AiQuery("admin.cli.status", UserId, Input: JsonSerializer.SerializeToElement(new { provider })), ct));
    [HttpPost("cli/{provider}/login")] public async Task<IActionResult> CliLogin(string provider, JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.cli.login", UserId, Input: JsonSerializer.SerializeToElement(new { provider, acceptedRisk = input.TryGetProperty("acceptedRisk", out var accepted) && accepted.ValueKind == JsonValueKind.True })), ct));
    [HttpGet("cli/{provider}/login/{sessionId}")] public async Task<IActionResult> CliProgress(string provider, string sessionId, CancellationToken ct) => Ok(await mediator.Send(new AiQuery("admin.cli.progress", UserId, Input: JsonSerializer.SerializeToElement(new { provider, sessionId })), ct));
    [HttpPost("cli/{provider}/login/{sessionId}/complete")] public async Task<IActionResult> CliComplete(string provider, string sessionId, JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.cli.complete", UserId, Input: JsonSerializer.SerializeToElement(new { provider, sessionId, code = input.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null })), ct));
    [HttpDelete("cli/{provider}/login/{sessionId}")] public async Task<IActionResult> CliCancel(string provider, string sessionId, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.cli.cancel", UserId, Input: JsonSerializer.SerializeToElement(new { provider, sessionId })), ct));
    [HttpPost("connections")] public async Task<IActionResult> Create(JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.saveConnection", UserId, Input: input), ct));
    [HttpPut("connections/{id:guid}")] public async Task<IActionResult> Update(Guid id, JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.saveConnection", UserId, id, input), ct));
    [HttpDelete("connections/{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new AiCommand("admin.deleteConnection", UserId, id), ct); return NoContent(); }
    [HttpPost("connections/{id:guid}/test")] public async Task<IActionResult> Test(Guid id, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.testConnection", UserId, id), ct));
    [HttpPost("connections/{id:guid}/activate")] public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.activateConnection", UserId, id), ct));
    [HttpPost("connections/{id:guid}/oauth/start")] public async Task<IActionResult> OAuthStart(Guid id, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.oauthStart", UserId, id), ct));
    [HttpPost("connections/{id:guid}/oauth/complete")] public async Task<IActionResult> OAuthComplete(Guid id, JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.oauthComplete", UserId, id, input), ct));
    [HttpGet("settings")] public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await mediator.Send(new AiQuery("admin.settings", UserId), ct));
    [HttpPut("settings")] public async Task<IActionResult> SaveSettings(JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("admin.saveSettings", UserId, Input: input), ct));
    [HttpGet("usage")] public async Task<IActionResult> Usage([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? groupBy, CancellationToken ct)
        => Ok(await mediator.Send(new AiQuery("admin.usage", UserId, Input: JsonSerializer.SerializeToElement(new { from, to, groupBy })), ct));
}

[ApiController, Authorize(Policy = "InternalUser"), Route("api/organizations/current/ai")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationAiController(IMediator mediator) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await mediator.Send(new AiQuery("org.settings", UserId), ct));
    [HttpPut] public async Task<IActionResult> Save(JsonElement input, CancellationToken ct) => Ok(await mediator.Send(new AiCommand("org.saveSettings", UserId, Input: input), ct));
    [HttpGet("usage")] public async Task<IActionResult> Usage([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? groupBy, CancellationToken ct)
        => Ok(await mediator.Send(new AiQuery("org.usage", UserId, Input: JsonSerializer.SerializeToElement(new { from, to, groupBy })), ct));
}
