using System.ComponentModel.DataAnnotations;
using Ya.Events.WebApi.Attributes;

namespace Ya.Events.WebApi.DTOs.Requests;

public record CreateEventRequest
{
    [Required(ErrorMessage = "Название события не может быть пустым.")]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Дата начала события не может быть пустым.")]
    public DateTime? StartAt { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Дата окончания события не может быть пустым.")]
    [EndAfterStart(ErrorMessage = "Дата окончания должна быть позже даты начала.")]
    public DateTime? EndAt { get; set; }
}
