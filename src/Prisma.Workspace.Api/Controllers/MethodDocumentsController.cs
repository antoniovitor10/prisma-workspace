using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodDocuments;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Documentos do método (HU-0008): épico, HU, PRD e corpo da task. Aceita o token do método e a sessão humana.
/// O PRD usa o segmento <c>/prd</c> na rota (chave lógica <c>HU-0001/prd</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method/documents")]
public sealed class MethodDocumentsController(IMethodDocumentOperations documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MethodDocumentSummaryDto>>> List(Guid projectId,
        [FromQuery] DateTimeOffset? desde, [FromQuery] Guid? workItemId, CancellationToken ct)
        => Ok(await documents.ListAsync(projectId, desde, workItemId, ct));

    [HttpGet("{key}")]
    public async Task<ActionResult<MethodDocumentDto>> Get(Guid projectId, string key, CancellationToken ct)
        => Ok(await documents.GetAsync(projectId, key, ct));

    [HttpGet("{key}/prd")]
    public async Task<ActionResult<MethodDocumentDto>> GetPrd(Guid projectId, string key, CancellationToken ct)
        => Ok(await documents.GetAsync(projectId, Prd(key), ct));

    [HttpGet("{key}/revisions")]
    public async Task<ActionResult<IReadOnlyList<MethodDocumentRevisionDto>>> Revisions(Guid projectId, string key, CancellationToken ct)
        => Ok(await documents.RevisionsAsync(projectId, key, ct));

    [HttpGet("{key}/prd/revisions")]
    public async Task<ActionResult<IReadOnlyList<MethodDocumentRevisionDto>>> PrdRevisions(Guid projectId, string key, CancellationToken ct)
        => Ok(await documents.RevisionsAsync(projectId, Prd(key), ct));

    [HttpGet("{key}/revisions/{version:int}")]
    public async Task<ActionResult<MethodDocumentRevisionDto>> Revision(Guid projectId, string key, int version, CancellationToken ct)
        => Ok(await documents.RevisionAsync(projectId, key, version, ct));

    [HttpGet("{key}/prd/revisions/{version:int}")]
    public async Task<ActionResult<MethodDocumentRevisionDto>> PrdRevision(Guid projectId, string key, int version, CancellationToken ct)
        => Ok(await documents.RevisionAsync(projectId, Prd(key), version, ct));

    [HttpPut("{key}")]
    public async Task<ActionResult<MethodDocumentDto>> Save(Guid projectId, string key, SaveMethodDocumentRequest request, CancellationToken ct)
        => Ok(await documents.SaveAsync(projectId, key, request, ct));

    [HttpPut("{key}/prd")]
    public async Task<ActionResult<MethodDocumentDto>> SavePrd(Guid projectId, string key, SaveMethodDocumentRequest request, CancellationToken ct)
        => Ok(await documents.SaveAsync(projectId, Prd(key), request, ct));

    [HttpPatch("{key}/header")]
    public async Task<ActionResult<MethodDocumentDto>> UpdateHeader(Guid projectId, string key, UpdateMethodDocumentHeaderRequest request, CancellationToken ct)
        => Ok(await documents.UpdateHeaderAsync(projectId, key, request, ct));

    [HttpPost("tasks/{taskId}/parent")]
    public async Task<ActionResult<MethodTaskParentDto>> Relink(Guid projectId, string taskId, RelinkMethodTaskRequest request, CancellationToken ct)
        => Ok(await documents.RelinkTaskAsync(projectId, taskId, request, ct));

    private static string Prd(string key) => key + "/prd";
}
