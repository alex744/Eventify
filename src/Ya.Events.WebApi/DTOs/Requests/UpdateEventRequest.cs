using System.ComponentModel;
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

    [Required(ErrorMessage = "Общее количество мест обязательно.")]
    [Range(1, int.MaxValue, ErrorMessage = "Количество мест должно быть положительным.")]
    [DefaultValue(1)]
    public int? TotalSeats { get; set; }

    public string? Description { get; set; }
}
