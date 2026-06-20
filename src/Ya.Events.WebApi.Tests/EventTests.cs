using Ya.Events.Domain.Entities;

namespace Ya.Events.WebApi.Tests;

public class EventTests
{
    /// <summary>
    /// Проверяет, что при создании события название обрезается от пробелов в начале и конце.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void Title_WithWhitespace_TrimsTitleAndCreatesEvent()
    {
        // Arrange & Act
        var futureDate = DateTime.UtcNow.AddDays(1);
        var createEvent = new Event(
            title: "  Test Event  ",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 10);

        // Assert
        Assert.Equal("Test Event", createEvent.Title);
    }

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
        var futureDate = DateTime.UtcNow.AddDays(1);
        var exception1 = Assert.Throws<ArgumentException>(() => new Event("", futureDate, futureDate.AddHours(2), 10, "Описание"));
        var exception2 = Assert.Throws<ArgumentException>(() => new Event("     ", futureDate, futureDate.AddHours(2), 10, "Описание"));
        var exception3 = Assert.Throws<ArgumentException>(() => new Event("Корректный заголовок", futureDate.AddHours(2), futureDate, 1, "Описание"));
        var exception4 = Assert.Throws<ArgumentException>(() => new Event("Заголовок", futureDate, futureDate.AddHours(2), 0, "Описание"));
        var exception5 = Assert.Throws<ArgumentException>(() => new Event("Заголовок", futureDate, futureDate.AddHours(2), -5, "Описание"));

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception1.Message);
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception2.Message);
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception3.Message);
        Assert.Equal("Общее количество мест должно быть положительным. (Parameter 'TotalSeats')", exception4.Message);
        Assert.Equal("Общее количество мест должно быть положительным. (Parameter 'TotalSeats')", exception5.Message);
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
        var futureDate = DateTime.UtcNow.AddDays(1);
        var newEvent = new Event("Событие", futureDate, futureDate.AddHours(2), 10, "Описание");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => newEvent.Title = "");

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception.Message);
        Assert.Equal("Событие", newEvent.Title);
    }

    /// <summary>
    /// Проверяет, что при создании события с датой начала в прошлом выбрасывается ArgumentException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void SetStartAt_WhenInPast_ThrowsArgumentException()
    {
        // Arrange
        var nowDate = DateTime.UtcNow.AddDays(1);
        var createEvent = new Event("Test Event", nowDate, nowDate.AddHours(2), 10);

        // Act        
        var exception = Assert.Throws<ArgumentException>(() => createEvent.StartAt = nowDate.AddDays(-1));

        // Assert        
        Assert.Equal("Дата начала не может быть в прошлом. (Parameter 'StartAt')", exception.Message);
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
        var futureDate = DateTime.UtcNow.AddDays(1);
        var newEvent = new Event("Событие", futureDate, futureDate.AddHours(2), 10, "Описание");

        // Act        
        var exception = Assert.Throws<ArgumentException>(() => newEvent.EndAt = futureDate);

        // Assert        
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception.Message);
        Assert.Equal("Событие", newEvent.Title);
    }
}
