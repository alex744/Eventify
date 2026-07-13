using Ya.Events.Domain.Entities;

namespace Ya.Events.WebApi.Tests;

public class EventTests
{
    /// <summary>
    /// Проверяет, что при создании события название обрезается от пробелов в начале и конце.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void Create_WithWhitespace_TrimsTitleAndCreatesEvent()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act
        var createEvent = Event.Create(
            title: "  Test Event  ",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 10);

        // Assert
        Assert.Equal("Test Event", createEvent.Title);
    }

    /// <summary>
    /// Проверяет, что метод Create выбрасывает исключение ArgumentException
    /// при передаче некорректных данных:
    /// - пустой или состоящий из пробелов заголовок;
    /// - дата окончания раньше или равна дате начала.
    /// - количество мест меньше или равно нулю.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Create_WithInvalidData_ThrowsArgumentException()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act
        var exception1 = Assert.Throws<ArgumentException>(() => Event.Create("", futureDate, futureDate.AddHours(2), 10));
        var exception2 = Assert.Throws<ArgumentException>(() => Event.Create("     ", futureDate, futureDate.AddHours(2), 10));
        var exception3 = Assert.Throws<ArgumentException>(() => Event.Create("Корректный заголовок", futureDate.AddHours(2), futureDate, 1));
        var exception4 = Assert.Throws<ArgumentException>(() => Event.Create("Заголовок", futureDate, futureDate.AddHours(2), 0));
        var exception5 = Assert.Throws<ArgumentException>(() => Event.Create("Заголовок", futureDate, futureDate.AddHours(2), -5));

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception1.Message);
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception2.Message);
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception3.Message);
        Assert.Equal("Общее количество мест должно быть больше нуля. (Parameter 'TotalSeats')", exception4.Message);
        Assert.Equal("Общее количество мест должно быть больше нуля. (Parameter 'TotalSeats')", exception5.Message);
    }

    /// <summary>
    /// Проверяет, что при попытке обновить событие с пустым заголовком
    /// выбрасывается исключение ArgumentException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Update_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange        
        var startAt = DateTime.UtcNow.AddDays(1);
        var endAt = startAt.AddHours(2);
        var newEvent = Event.Create("Событие", startAt, endAt, 10, "Описание");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => newEvent.Update("", startAt, endAt));

        // Assert
        Assert.Equal("Название события обязательно. (Parameter 'Title')", exception.Message);
        Assert.Equal("Событие", newEvent.Title);
    }

    /// <summary>
    /// Проверяет, что при создании события с датой начала в прошлом 
    /// выбрасывается исключение ArgumentException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Create_WithStartDateInPast_ThrowsArgumentException()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act        
        var exception = Assert.Throws<ArgumentException>(() => Event.Create("Test Event", pastDate, futureDate, 10));

        // Assert        
        Assert.Equal("Дата начала не может быть в прошлом. (Parameter 'StartAt')", exception.Message);
    }

    /// <summary>
    /// Проверяет, что при обновлении события с датой окончания, меньшей или равной дате начала,
    /// выбрасывается исключение ArgumentException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Update_WithEndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange        
        var futureDate = DateTime.UtcNow.AddDays(1);
        var newEvent = Event.Create("Событие", futureDate, futureDate.AddHours(2), 10, "Описание");

        // Act        
        var exception = Assert.Throws<ArgumentException>(() => newEvent.Update("Событие", futureDate, futureDate));

        // Assert        
        Assert.Equal("Дата окончания должна быть позже даты начала. (Parameter 'EndAt')", exception.Message);
        Assert.Equal("Событие", newEvent.Title);
    }
}
