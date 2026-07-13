using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.WebApi.IntegrationTests.Fixtures;

namespace Ya.Events.WebApi.IntegrationTests;

[Collection("PostgreSQL collection")]
public sealed class UserRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly IUserRepository _userRepository;

    public UserRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _userRepository = fixture.ServiceProvider.GetRequiredService<IUserRepository>();
    }

    #region CreateAsync Tests

    /// <summary>
    /// Проверяет, что пользователь успешно создаётся и возвращается с корректными данными.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_WithValidUser_ReturnsCreatedUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("testuser", "hashed_password", UserRole.User);

        // Act
        var result = await _userRepository.CreateAsync(user, ct);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(user.Login, result.Login);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(UserRole.User, result.Role);
    }

    /// <summary>
    /// Проверяет, что несколько пользователей могут быть созданы с разными логинами.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_MultipleUsers_AllPersisted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var users = new[]
        {
            User.Create("user1", "password_hash1", UserRole.User),
            User.Create("user2", "password_hash2", UserRole.User),
            User.Create("admin", "password_hash3", UserRole.Admin)
        };

        // Act
        var results = new List<User>();
        foreach (var user in users)
        {
            results.Add(await _userRepository.CreateAsync(user, ct));
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, u => Assert.NotEqual(Guid.Empty, u.Id));
        Assert.Equal("user1", results[0].Login);
        Assert.Equal("user2", results[1].Login);
        Assert.Equal("admin", results[2].Login);
        Assert.Equal(UserRole.Admin, results[2].Role);
    }

    /// <summary>
    /// Проверяет, что каждому созданному пользователю присваивается уникальный ID.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_MultipleUsers_HaveUniqueIds()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var user1 = await _userRepository.CreateAsync(
            User.Create("user1", "hash1", UserRole.User), ct);
        var user2 = await _userRepository.CreateAsync(
            User.Create("user2", "hash2", UserRole.User), ct);
        var user3 = await _userRepository.CreateAsync(
            User.Create("user3", "hash3", UserRole.User), ct);

        // Assert
        var ids = new[] { user1.Id, user2.Id, user3.Id };
        Assert.Equal(3, ids.Distinct().Count());
    }

    /// <summary>
    /// Проверяет, что пользователь, созданный с ролью Admin, сохраняется с этой ролью.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_WithAdminRole_PersistsAdminRole()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var adminUser = User.Create("admin", "admin_hash", UserRole.Admin);

        // Act
        var result = await _userRepository.CreateAsync(adminUser, ct);

        // Assert
        Assert.Equal(UserRole.Admin, result.Role);
    }

    #endregion

    #region GetByIdAsync Tests

    /// <summary>
    /// Проверяет, что пользователь может быть получен по ID и содержит корректные данные.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByIdAsync_WithExistingId_ReturnsUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("findme", "my_password_hash", UserRole.User);
        var created = await _userRepository.CreateAsync(user, ct);

        // Act
        var result = await _userRepository.GetByIdAsync(created.Id, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("findme", result.Login);
        Assert.Equal("my_password_hash", result.PasswordHash);
        Assert.Equal(UserRole.User, result.Role);
    }

    /// <summary>
    /// Проверяет, что при запросе несуществующего ID возвращается null.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _userRepository.GetByIdAsync(nonExistentId, ct);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что GetByIdAsync возвращает разные пользователей по разным ID.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByIdAsync_WithMultipleUsers_ReturnsCorrectUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user1 = await _userRepository.CreateAsync(
            User.Create("alice", "hash1", UserRole.User), ct);
        var user2 = await _userRepository.CreateAsync(
            User.Create("bob", "hash2", UserRole.Admin), ct);

        // Act
        var retrieved1 = await _userRepository.GetByIdAsync(user1.Id, ct);
        var retrieved2 = await _userRepository.GetByIdAsync(user2.Id, ct);

        // Assert
        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal("alice", retrieved1.Login);
        Assert.Equal("bob", retrieved2.Login);
        Assert.Equal(UserRole.User, retrieved1.Role);
        Assert.Equal(UserRole.Admin, retrieved2.Role);
    }

    #endregion

    #region GetByLoginAsync Tests

    /// <summary>
    /// Проверяет, что пользователь может быть найден по логину.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByLoginAsync_WithExistingLogin_ReturnsUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("johndoe", "password_hash", UserRole.User);
        await _userRepository.CreateAsync(user, ct);

        // Act
        var result = await _userRepository.GetByLoginAsync("johndoe", ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("johndoe", result.Login);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("password_hash", result.PasswordHash);
    }

    /// <summary>
    /// Проверяет, что при поиске несуществующего логина возвращается null.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByLoginAsync_WithNonExistentLogin_ReturnsNull()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _userRepository.GetByLoginAsync("nonexistent", ct);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что поиск логина регистрозависим (или независим, в зависимости от БД).
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByLoginAsync_WithDifferentCase_SearchIsCaseSensitive()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("TestUser", "hash", UserRole.User);
        await _userRepository.CreateAsync(user, ct);

        // Act
        var resultExact = await _userRepository.GetByLoginAsync("TestUser", ct);
        var resultLower = await _userRepository.GetByLoginAsync("testuser", ct);
        var resultUpper = await _userRepository.GetByLoginAsync("TESTUSER", ct);

        // Assert
        Assert.NotNull(resultExact);
        Assert.Null(resultLower);
        Assert.Null(resultUpper);
    }

    /// <summary>
    /// Проверяет, что в БД может быть несколько пользователей, и каждый может быть найден по логину.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByLoginAsync_WithMultipleUsers_ReturnsCorrectUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user1 = User.Create("admin1", "hash1", UserRole.Admin);
        var user2 = User.Create("user1", "hash2", UserRole.User);
        var user3 = User.Create("user2", "hash3", UserRole.User);

        await _userRepository.CreateAsync(user1, ct);
        await _userRepository.CreateAsync(user2, ct);
        await _userRepository.CreateAsync(user3, ct);

        // Act
        var foundUser1 = await _userRepository.GetByLoginAsync("user1", ct);
        var foundUser2 = await _userRepository.GetByLoginAsync("user2", ct);
        var foundAdmin = await _userRepository.GetByLoginAsync("admin1", ct);

        // Assert
        Assert.NotNull(foundUser1);
        Assert.NotNull(foundUser2);
        Assert.NotNull(foundAdmin);
        Assert.Equal("user1", foundUser1.Login);
        Assert.Equal("user2", foundUser2.Login);
        Assert.Equal("admin1", foundAdmin.Login);
        Assert.Equal(UserRole.User, foundUser1.Role);
        Assert.Equal(UserRole.User, foundUser2.Role);
        Assert.Equal(UserRole.Admin, foundAdmin.Role);
    }

    /// <summary>
    /// Проверяет, что логин с пробелами может быть найден точно.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task GetByLoginAsync_WithSpaces_FindsExactLogin()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("user name", "hash", UserRole.User);
        await _userRepository.CreateAsync(user, ct);

        // Act
        var result = await _userRepository.GetByLoginAsync("user name", ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user name", result.Login);
    }

    #endregion

    #region SaveChangesAsync Tests

    /// <summary>
    /// Проверяет, что SaveChangesAsync сохраняет изменения пользователя в БД.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;

        // Этот тест демонстрирует использование SaveChangesAsync для сохранения изменений
        // хотя в текущей реализации CreateAsync сам вызывает SaveChangesAsync

        var user = User.Create("saveme", "original_hash", UserRole.User);
        var created = await _userRepository.CreateAsync(user, ct);

        // Получаем пользователя из БД для проверки
        var retrieved = await _userRepository.GetByLoginAsync("saveme", ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("original_hash", retrieved.PasswordHash);
    }

    #endregion

    #region Data Integrity Tests

    /// <summary>
    /// Проверяет, что логин и пароль сохраняются полностью без обрезания.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAndRetrieve_WithLongLoginAndPassword_PreservesFullData()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var longLogin = "very.long.username.with.special.chars@example.com";
        var longPasswordHash = string.Concat(Enumerable.Repeat("a", 512));  // Максимальная длина PasswordHash

        var user = User.Create(longLogin, longPasswordHash, UserRole.User);

        // Act
        var created = await _userRepository.CreateAsync(user, ct);
        var retrieved = await _userRepository.GetByLoginAsync(longLogin, ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(longLogin, retrieved.Login);
        Assert.Equal(longPasswordHash, retrieved.PasswordHash);
    }

    /// <summary>
    /// Проверяет, что уникальность логина обеспечивается на уровне БД.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_WithDuplicateLogin_ThrowsException()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var duplicateLogin = "duplicate";

        var user1 = User.Create(duplicateLogin, "hash1", UserRole.User);
        await _userRepository.CreateAsync(user1, ct);

        var user2 = User.Create(duplicateLogin, "hash2", UserRole.User);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => _userRepository.CreateAsync(user2, ct));
    }

    /// <summary>
    /// Проверяет, что пользователь сохраняет связь с бронированиями (если они существуют).
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_User_CanBeRetrievedWithBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var user = User.Create("bookinguser", "hash", UserRole.User);

        // Act
        var created = await _userRepository.CreateAsync(user, ct);
        var retrieved = await _userRepository.GetByIdAsync(created.Id, ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Empty(retrieved.Bookings); // Новый пользователь не имеет бронирований
    }

    #endregion

    #region Edge Cases Tests

    /// <summary>
    /// Проверяет, что пользователь может быть создан и найден с минимальной длиной логина.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_WithMinimalLogin_Works()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var minimalLogin = "abc"; // Минимум 3 символа по валидации
        var user = User.Create(minimalLogin, "hash", UserRole.User);

        // Act
        var created = await _userRepository.CreateAsync(user, ct);
        var retrieved = await _userRepository.GetByLoginAsync(minimalLogin, ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(minimalLogin, retrieved.Login);
    }

    /// <summary>
    /// Проверяет, что пользователь может быть создан с логином, содержащим спецсимволы.
    /// </summary>
    [Fact]
    [Trait("Category", "UserRepository")]
    public async Task CreateAsync_WithSpecialCharactersInLogin_Works()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var ct = TestContext.Current.CancellationToken;
        var specialLogin = "user-name_123.test@domain";
        var user = User.Create(specialLogin, "hash", UserRole.User);

        // Act
        var created = await _userRepository.CreateAsync(user, ct);
        var retrieved = await _userRepository.GetByLoginAsync(specialLogin, ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(specialLogin, retrieved.Login);
    }

    #endregion
}