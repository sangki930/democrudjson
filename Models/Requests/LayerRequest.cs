using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DemoCrud.Api.Models.Requests;

public class LayerDataRequest : IValidatableObject
{
    public JsonElement Data { get; init; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Data.ValueKind != JsonValueKind.Object)
            yield return new ValidationResult("Data must be a JSON object.", ["data"]);
    }
}

public sealed class CreateLayerRequest : LayerDataRequest
{
    public string? Id { get; init; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var error in base.Validate(context)) yield return error;
        if (Id is null || Id.Length != 24 || !Id.All(char.IsAsciiHexDigit))
            yield return new ValidationResult("Id must be a 24-character hexadecimal MongoDB ObjectId.", ["id"]);
    }
}

