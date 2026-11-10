using System.Text.Json;

namespace DemoCrud.Api.Models.Entities;

public sealed class Layer
{
    public string Id { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
}
