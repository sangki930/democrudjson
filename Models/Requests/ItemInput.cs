using System.ComponentModel.DataAnnotations;

namespace DemoCrud.Api.Models.Requests;

public sealed class ItemInput : IValidatableObject
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 200)
            yield return new ValidationResult("Name is required and must be at most 200 characters.", ["name"]);
        if (Description?.Length > 2000)
            yield return new ValidationResult("Description must be at most 2000 characters.", ["description"]);
    }
}
