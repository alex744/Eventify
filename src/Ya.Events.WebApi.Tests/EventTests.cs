using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Tests;

public class EventTests
{
    /// <summary>
    /// Проверяет, что конструктор класса Event выбрасывает исключение ArgumentException
    /// при передаче некорректных данных:
    /// - пустой или состоящий из пробелов заголовок;
    /// - дата окончания раньше или равна дате начала.
    /// - количество мест меньше или равно нулю.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Constructor_WithInvalidData_ThrowsArgumentException()
    {
        // Act
        var exception1 = Assert.Throws<ArgumentException>(() => new Event("", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), 10, "Описание"));
        var exception2 = Assert.Throws<ArgumentException>(() => new Event("Корректный заголовок", new DateTime(2026, 1, 2), new DateTime(2026, 1, 1), 1, "Описание"));
        var exception3 = Assert.Throws<ArgumentException>(() => new Event("Заголовок", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), 0, "Описание"));
        var exception4 = Assert.Throws<ArgumentException>(() => new Event("Заголовок", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), -5, "Описание"));

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception1.Message);
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception2.Message);
        Assert.Equal("Общее количество мест должно быть положительным. (Parameter 'TotalSeats')", exception3.Message);
        Assert.Equal("Общее количество мест должно быть положительным. (Parameter 'TotalSeats')", exception4.Message);
    }

    /// <summary>
    /// Проверяет, что при попытке присвоить свойству Title пустую строку или null
    /// выбрасывается исключение ArgumentException с сообщением о том, что название события обязательно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Title_WhenIsNullOrEmpty_ThrowsArgumentException()
    {
        // Arrange
        var originalTitle = "Событие";
        var newEvent = new Event(originalTitle, new DateTime(2026, 1, 2), new DateTime(2026, 1, 3), 10, "Описание");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => newEvent.Title = "");

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception.Message);
        Assert.Equal(originalTitle, newEvent.Title);
    }

    /// <summary>
    /// Проверяет, что при попытке установить свойству EndAt значение,
    /// которое меньше или равно текущему StartAt, выбрасывается исключение ArgumentException
    /// с соответствующим сообщением.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void SetEndAt_WhenEndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange
        var originalTitle = "Событие";
        var newEvent = new Event(originalTitle, new DateTime(2026, 1, 2), new DateTime(2026, 1, 3), 10, "Описание");

        // Act        
        var exception = Assert.Throws<ArgumentException>(() => newEvent.EndAt = new DateTime(2026, 1, 1));

        // Assert        
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception.Message);
        Assert.Equal(originalTitle, newEvent.Title);
    }
}
