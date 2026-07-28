using Moq;
using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Services;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Tests;

public sealed class EventServiceCacheTests
{
    /// <summary>
    /// Проверяет, что при наличии данных в кэше метод GetByIdAsync не вызывает репозиторий и возвращает данные из кэша.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "CacheHit")]
    public async Task GetByIdAsync_WhenCacheHit_RepositoryIsNotCalled()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out _);

        var id = Guid.NewGuid();
        var cacheKey = $"event:{id}";
        var cachedEvent = CreateValidEvent();

        cacheMock
            .Setup(c => c.GetAsync<Event>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEvent);

        // Act
        var result = await service.GetByIdAsync(id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        cacheMock.Verify(c => c.SetAsync<Event>(It.IsAny<string>(), It.IsAny<Event>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Проверяет, что при отсутствии данных в кэше метод GetByIdAsync вызывает репозиторий для получения данных и сохраняет их в кэш.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "CacheMiss")]
    public async Task GetByIdAsync_WhenCacheMiss_LoadsFromRepository_AndStoresInCache()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out var ttlMock);

        var id = Guid.NewGuid();
        var cacheKey = $"event:{id}";
        var expectedTtl = TimeSpan.FromMinutes(5);
        var fromRepository = CreateValidEvent();

        ttlMock.SetupGet(t => t.EventByIdTtl).Returns(expectedTtl);

        cacheMock
            .Setup(c => c.GetAsync<Event>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fromRepository);

        // Act
        var result = await service.GetByIdAsync(id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        repositoryMock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.SetAsync(cacheKey, It.Is<Event>(e => ReferenceEquals(e, fromRepository)), expectedTtl, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Проверяет, что при наличии данных в кэше метод GetTopEventsAsync не вызывает репозиторий и возвращает данные из кэша.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "CacheHit")]
    public async Task GetTopEventsAsync_WhenCacheHit_RepositoryIsNotCalled()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out _);

        const string cacheKey = "events:top10";
        IReadOnlyList<Event> cachedTopEvents = [CreateValidEvent(), CreateValidEvent()];

        cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<Event>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTopEvents);

        // Act
        var result = await service.GetTopEventsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        repositoryMock.Verify(r => r.GetTop10Async(It.IsAny<CancellationToken>()), Times.Never);
        cacheMock.Verify(c => c.SetAsync<IReadOnlyList<Event>>(It.IsAny<string>(), It.IsAny<IReadOnlyList<Event>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Проверяет, что при отсутствии данных в кэше метод GetTopEventsAsync вызывает репозиторий для получения данных и сохраняет их в кэш.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "CacheMiss")]
    public async Task GetTopEventsAsync_WhenCacheMiss_LoadsFromRepository_AndStoresInCache()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out var ttlMock);

        const string cacheKey = "events:top10";
        var expectedTtl = TimeSpan.FromMinutes(1);
        IReadOnlyList<Event> topEventsFromRepository = [CreateValidEvent(), CreateValidEvent(), CreateValidEvent()];

        ttlMock.SetupGet(t => t.TopEventsTtl).Returns(expectedTtl);

        cacheMock
            .Setup(c => c.GetAsync<IReadOnlyList<Event>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Event>?)null);

        repositoryMock
            .Setup(r => r.GetTop10Async(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topEventsFromRepository);

        // Act
        var result = await service.GetTopEventsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        repositoryMock.Verify(r => r.GetTop10Async(It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.SetAsync(cacheKey, It.Is<IReadOnlyList<Event>>(events => ReferenceEquals(events, topEventsFromRepository)), expectedTtl, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Проверяет, что при успешном обновлении события метод UpdateAsync вызывает репозиторий для обновления данных и инвалидирует кэш по идентификатору события.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "WriteInvalidation")]
    public async Task UpdateAsync_WhenSuccess_InvalidatesCacheById()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out _);

        var id = Guid.NewGuid();
        var cacheKey = $"event:{id}";
        var updated = CreateValidEvent();

        repositoryMock
            .Setup(r => r.UpdateAsync(id, updated, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await service.UpdateAsync(id, updated, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        repositoryMock.Verify(r => r.UpdateAsync(id, updated, It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Проверяет, что при успешном удалении события метод DeleteAsync вызывает репозиторий для удаления данных и инвалидирует кэш по идентификатору события.
    /// </summary>    
    [Fact]
    [Trait("Scenario", "WriteInvalidation")]
    public async Task DeleteAsync_WhenSuccess_InvalidatesCacheById()
    {
        // Arrange
        var service = CreateService(out var cacheMock, out var repositoryMock, out _);

        var id = Guid.NewGuid();
        var cacheKey = $"event:{id}";
        var existing = CreateValidEvent();

        repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        repositoryMock
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await service.DeleteAsync(id, TestContext.Current.CancellationToken);

        // Assert
        repositoryMock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EventService CreateService(
        out Mock<ICache> cacheMock,
        out Mock<IEventRepository> repositoryMock,
        out Mock<ICacheTtlProvider> ttlMock)
    {
        cacheMock = new Mock<ICache>();
        repositoryMock = new Mock<IEventRepository>();
        ttlMock = new Mock<ICacheTtlProvider>();

        ttlMock.SetupGet(t => t.EventByIdTtl).Returns(TimeSpan.FromMinutes(5));
        ttlMock.SetupGet(t => t.TopEventsTtl).Returns(TimeSpan.FromMinutes(1));

        return new EventService(cacheMock.Object, repositoryMock.Object, ttlMock.Object);
    }

    private static Event CreateValidEvent()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);

        return Event.Create(
            title: "Test Event",
            startAt: start,
            endAt: end,
            totalSeats: 10,
            description: "test");
    }
}
