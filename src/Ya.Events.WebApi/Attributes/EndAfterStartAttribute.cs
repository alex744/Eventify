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

        var model = validationContext.ObjectInstance;
        DateTime? startAt = null, endAt = value as DateTime?;

        switch (model)
        {
            case CreateEventRequest create:
                startAt = create.StartAt;
                endAt ??= create.EndAt;
                break;
            case UpdateEventRequest update:
                startAt = update.StartAt;
                endAt ??= update.EndAt;
                break;
            default:
                return ValidationResult.Success;
        }

        if (startAt.HasValue && endAt.HasValue && endAt <= startAt)
        {
            return new ValidationResult(ErrorMessage, [nameof(CreateEventRequest.EndAt)]);
        }

        return ValidationResult.Success;
    }
}
