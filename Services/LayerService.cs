using DemoCrud.Api.Data;
using DemoCrud.Api.Models.Entities;
using DemoCrud.Api.Models.Requests;
using DemoCrud.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DemoCrud.Api.Services;

public sealed class LayerService(AppDbContext db)
{
    public async Task<IReadOnlyList<LayerResponse>> ListAsync(CancellationToken ct)
    {
        var layers = await db.Layers.AsNoTracking().OrderBy(layer => layer.Id).Take(100).ToListAsync(ct);
        return layers.Select(LayerResponse.FromEntity).ToArray();
    }

    public async Task<LayerResponse?> GetAsync(string id, CancellationToken ct)
    {
        var layer = await db.Layers.AsNoTracking().SingleOrDefaultAsync(layer => layer.Id == id, ct);
        return layer is null ? null : LayerResponse.FromEntity(layer);
    }

    public async Task<LayerResponse?> CreateAsync(CreateLayerRequest request, CancellationToken ct)
    {
        var layer = new Layer { Id = request.Id!, Data = request.Data.Clone() };
        db.Layers.Add(layer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return null;
        }
        return LayerResponse.FromEntity(layer);
    }

    public async Task<LayerResponse?> UpdateAsync(string id, LayerDataRequest request, CancellationToken ct)
    {
        var layer = await db.Layers.SingleOrDefaultAsync(layer => layer.Id == id, ct);
        if (layer is null) return null;
        layer.Data = request.Data.Clone();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return null; }
        return LayerResponse.FromEntity(layer);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct) =>
        await db.Layers.Where(layer => layer.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<LayerResponse?> PatchAsync(string id, System.Text.Json.JsonElement patch, CancellationToken ct)
    {
        // Lock before reading so simultaneous PATCH requests merge against the latest committed JSON.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Layers.FromSqlInterpolated(
            $"SELECT id, data FROM democrud_demo.\"LAYER\" WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var layer = rows.SingleOrDefault();
        if (layer is null) return null;
        layer.Data = LayerJsonPatch.Apply(layer.Data, patch);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return LayerResponse.FromEntity(layer);
    }
}

