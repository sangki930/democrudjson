using DemoCrud.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoCrud.Api.Controllers;

[ApiController]
public sealed class SystemController(AppDbContext db) : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index() => Ok(new { name = "PostgreSQL CRUD test API", items = "/api/items", health = "/health" });

    [HttpGet("/health")]
    public async Task<IActionResult> Health(CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "healthy", database = "PostgreSQL" })
            : Problem("PostgreSQL connection unavailable.", statusCode: 503);
}
