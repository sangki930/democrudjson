using DemoCrud.Api.Models.Entities;

namespace DemoCrud.Api.Models.Responses;

public sealed record ItemResponse(Guid Id, string Name, string? Description,
    bool IsCompleted, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ItemResponse FromEntity(Item item) => new(item.Id, item.Name,
        item.Description, item.IsCompleted, item.CreatedAt, item.UpdatedAt);
}
