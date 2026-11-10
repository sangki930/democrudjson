using DemoCrud.Api.Models.Requests;
using DemoCrud.Api.Models.Responses;
using DemoCrud.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DemoCrud.Api.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController(ItemService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ItemResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemResponse>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpPost]
    public async Task<ActionResult<ItemResponse>> Create(ItemInput input, CancellationToken ct)
    {
        var item = await service.CreateAsync(input, ct);
        return Created($"/api/items/{item.Id}", item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ItemResponse>> Update(Guid id, ItemInput input, CancellationToken ct) =>
        await service.UpdateAsync(id, input, ct) is { } item ? Ok(item) : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
