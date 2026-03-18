using System.ComponentModel.DataAnnotations;
using Ya.Events.WebApi.DTOs.Requests;

namespace Ya.Events.WebApi.Attributes;

public class EndAfterStartAttribute : ValidationAttribute
{
    public EndAfterStartAttribute()
    {
        ErrorMessage = "The EndAt must be later than the StartAt.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Null-значения должны обрабатываться атрибутом [Required]
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (validationContext.ObjectInstance is CreateEventRequest model)
        {
            if (model.EndAt <= model.StartAt)
            {
                return new ValidationResult(ErrorMessage, [nameof(CreateEventRequest.EndAt)]);
            }
        }

        return ValidationResult.Success;
    }
}
