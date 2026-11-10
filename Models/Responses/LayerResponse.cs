using System.Text.Json;
using DemoCrud.Api.Models.Entities;

namespace DemoCrud.Api.Models.Responses;

public sealed record LayerResponse(string Id, JsonElement Data)
{
    public static LayerResponse FromEntity(Layer layer) => new(layer.Id, layer.Data);
}
