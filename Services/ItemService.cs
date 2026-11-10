using DemoCrud.Api.Data;
using DemoCrud.Api.Models.Entities;
using DemoCrud.Api.Models.Requests;
using DemoCrud.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace DemoCrud.Api.Services;

public sealed class ItemService(AppDbContext db)
{
    public async Task<IReadOnlyList<ItemResponse>> ListAsync(CancellationToken ct)
    {
        var items = await db.Items.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id).Take(100).ToListAsync(ct);
        return items.Select(ItemResponse.FromEntity).ToArray();
    }

    public async Task<ItemResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return item is null ? null : ItemResponse.FromEntity(item);
    }

    public async Task<ItemResponse> CreateAsync(ItemInput input, CancellationToken ct)
    {
        var item = new Item { Id = Guid.NewGuid(), Name = input.Name!.Trim(),
            Description = input.Description, IsCompleted = input.IsCompleted };
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return ItemResponse.FromEntity(item);
    }

    public async Task<ItemResponse?> UpdateAsync(Guid id, ItemInput input, CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (item is null) return null;
        item.Name = input.Name!.Trim();
        item.Description = input.Description;
        item.IsCompleted = input.IsCompleted;
        item.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            // A delete between SELECT and UPDATE is also a missing ID.
            return null;
        }
        return ItemResponse.FromEntity(item);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct) =>
        await db.Items.Where(item => item.Id == id).ExecuteDeleteAsync(ct) > 0;
}
