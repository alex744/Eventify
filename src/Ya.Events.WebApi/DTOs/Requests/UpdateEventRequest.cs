using System.ComponentModel.DataAnnotations;
using Ya.Events.WebApi.Attributes;

namespace Ya.Events.WebApi.DTOs.Requests;

public record UpdateEventRequest
{
    [Required(ErrorMessage = "Название события обязательно.")]
    public required string Title { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Дата начала события обязательна.")]
    public DateTime? StartAt { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Дата окончания события обязательна.")]
    [EndAfterStart(ErrorMessage = "Дата окончания должна быть позже даты начала.")]
    public DateTime? EndAt { get; set; }

    public string? Description { get; set; }
}
