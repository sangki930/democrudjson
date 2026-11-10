using DemoCrud.Api.Models.Requests;
using DemoCrud.Api.Models.Responses;
using DemoCrud.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DemoCrud.Api.Controllers;

[ApiController]
[Route("api/layers")]
public sealed class LayersController(LayerService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LayerResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<LayerResponse>> Get(string id, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } layer ? Ok(layer) : NotFound();

    [HttpPost]
    public async Task<ActionResult<LayerResponse>> Create(CreateLayerRequest request, CancellationToken ct)
    {
        var layer = await service.CreateAsync(request, ct);
        return layer is null
            ? Problem("A layer with this ID already exists.", statusCode: 409)
            : CreatedAtAction(nameof(Get), new { id = layer.Id }, layer);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<LayerResponse>> Update(string id, LayerDataRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct) is { } layer ? Ok(layer) : NotFound();

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPatch("{id}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(1_048_576)]
    public async Task<ActionResult<LayerResponse>> Patch(string id,
        [FromBody] System.Text.Json.JsonElement request, CancellationToken ct)
    {
        try
        {
            return await service.PatchAsync(id, request, ct) is { } layer ? Ok(layer) : NotFound();
        }
        catch (LayerPatchException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }
}

