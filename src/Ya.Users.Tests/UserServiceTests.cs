using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.ComponentModel.DataAnnotations;
using Ya.Users.Application.Abstractions.Persistence.Repositories;
using Ya.Users.Application.Abstractions.Security;
using Ya.Users.Application.Abstractions.Services;
using Ya.Users.Application.DTOs;
using Ya.Users.Application.Services;
using Ya.Users.Domain.Exceptions;
using Ya.Users.Domain.ValueObjects;
using Ya.Users.Infrastructure.Persistence;
using Ya.Users.Infrastructure.Repositories;

namespace Ya.Users.Tests;

public sealed class UserServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IAccessTokenGenerator> _tokenGeneratorMock;

    public UserServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IUserRepository, UserRepository>();

        // Используем реальный PasswordHasher
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenGeneratorMock = new Mock<IAccessTokenGenerator>();

        services.AddScoped(_ => _passwordHasherMock.Object);
        services.AddScoped(_ => _tokenGeneratorMock.Object);
        services.AddScoped<IUserService, UserService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _userService = _scope.ServiceProvider.GetRequiredService<IUserService>();
        _userRepository = _scope.ServiceProvider.GetRequiredService<IUserRepository>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    #region RegisterAsync Tests

    /// <summary>
    /// Проверяет, что пользователь успешно регистрируется с корректными данными.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task RegisterAsync_WithValidRequest_CreatesUserSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "testuser",
            Password = "password123",
            Role = "User"
        };

        var passwordHash = "hashed_password";
        _passwordHasherMock.Setup(p => p.Hash(request.Password)).Returns(passwordHash);

        // Act
        await _userService.RegisterAsync(request, ct);

        // Assert
        var createdUser = await _userRepository.GetByLoginAsync(request.Login, ct);
        Assert.NotNull(createdUser);
        Assert.Equal(request.Login, createdUser.Login);
        Assert.Equal(UserRole.User, createdUser.Role);
        _passwordHasherMock.Verify(p => p.Hash(request.Password), Times.Once);
    }

    /// <summary>
    /// Проверяет, что при регистрации по умолчанию присваивается роль User.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task RegisterAsync_WithoutRole_AssignsUserRoleByDefault()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "defaultroleuser",
            Password = "password123"
        };

        var passwordHash = "hashed_password";
        _passwordHasherMock.Setup(p => p.Hash(request.Password)).Returns(passwordHash);

        // Act
        await _userService.RegisterAsync(request, ct);

        // Assert
        var createdUser = await _userRepository.GetByLoginAsync(request.Login, ct);
        Assert.NotNull(createdUser);
        Assert.Equal(UserRole.User, createdUser.Role);
    }

    /// <summary>
    /// Проверяет, что при регистрации можно присвоить роль Admin.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task RegisterAsync_WithAdminRole_CreatesAdminUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "adminuser",
            Password = "password123",
            Role = "Admin"
        };

        var passwordHash = "hashed_password";
        _passwordHasherMock.Setup(p => p.Hash(request.Password)).Returns(passwordHash);

        // Act
        await _userService.RegisterAsync(request, ct);

        // Assert
        var createdUser = await _userRepository.GetByLoginAsync(request.Login, ct);
        Assert.NotNull(createdUser);
        Assert.Equal(UserRole.Admin, createdUser.Role);
    }

    /// <summary>
    /// Проверяет, что при попытке регистрации с пустым логином выбрасывается исключение.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Scenario", "Failure")]
    public async Task RegisterAsync_WithEmptyLogin_ThrowsValidationException(string? login)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = login!,
            Password = "password123"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _userService.RegisterAsync(request, ct));
    }

    /// <summary>
    /// Проверяет, что при попытке регистрации с пустым паролем выбрасывается исключение.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Scenario", "Failure")]
    public async Task RegisterAsync_WithEmptyPassword_ThrowsValidationException(string? password)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "testuser",
            Password = password!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _userService.RegisterAsync(request, ct));
    }

    /// <summary>
    /// Проверяет, что при попытке регистрации с существующим логином выбрасывается исключение.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task RegisterAsync_WithExistingLogin_ThrowsValidationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var login = "existinguser";
        var passwordHash = "hashed_password";

        _passwordHasherMock.Setup(p => p.Hash(It.IsAny<string>())).Returns(passwordHash);

        var request1 = new RegisterUserRequest
        {
            Login = login,
            Password = "password123"
        };

        var request2 = new RegisterUserRequest
        {
            Login = login,
            Password = "password456"
        };

        // Act
        await _userService.RegisterAsync(request1, ct);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _userService.RegisterAsync(request2, ct));
        Assert.Contains("уже существует", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, что при попытке регистрации с некорректной ролью выбрасывается исключение.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task RegisterAsync_WithInvalidRole_ThrowsArgumentException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "testuser",
            Password = "password123",
            Role = "InvalidRole"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _userService.RegisterAsync(request, ct));
    }

    /// <summary>
    /// Проверяет, что логин обрезается от пробелов при регистрации.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task RegisterAsync_WithWhitespaceLogin_TrimsAndCreatesUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new RegisterUserRequest
        {
            Login = "  testuser  ",
            Password = "password123"
        };

        var passwordHash = "hashed_password";
        _passwordHasherMock.Setup(p => p.Hash(request.Password)).Returns(passwordHash);

        // Act
        await _userService.RegisterAsync(request, ct);

        // Assert
        var createdUser = await _userRepository.GetByLoginAsync("testuser", ct);
        Assert.NotNull(createdUser);
        Assert.Equal("testuser", createdUser.Login);
    }

    #endregion

    #region LoginAsync Tests

    /// <summary>
    /// Проверяет, что вход с корректными логином и паролем возвращает AuthResponse с токеном.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var login = "testuser";
        var password = "password123";
        var passwordHash = "hashed_password";
        var token = "jwt_token";

        _passwordHasherMock.Setup(p => p.Hash(password)).Returns(passwordHash);
        _passwordHasherMock.Setup(p => p.Verify(password, passwordHash)).Returns(true);
        _tokenGeneratorMock.Setup(t => t.CreateToken(It.IsAny<Guid>(), login, UserRole.User)).Returns(token);

        // Регистрируем пользователя
        var registerRequest = new RegisterUserRequest { Login = login, Password = password };
        await _userService.RegisterAsync(registerRequest, ct);

        // Act
        var loginRequest = new LoginUserRequest { Login = login, Password = password };
        var result = await _userService.LoginAsync(loginRequest, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(login, result.Login);
        Assert.Equal("User", result.Role);
        Assert.Equal(token, result.AccessToken);
        Assert.NotEqual(Guid.Empty, result.UserId);
    }

    /// <summary>
    /// Проверяет, что вход с неверным паролем выбрасывает NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task LoginAsync_WithWrongPassword_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var login = "testuser";
        var correctPassword = "password123";
        var wrongPassword = "wrongpassword";
        var passwordHash = "hashed_password";

        _passwordHasherMock.Setup(p => p.Hash(correctPassword)).Returns(passwordHash);
        _passwordHasherMock.Setup(p => p.Verify(wrongPassword, passwordHash)).Returns(false);

        // Регистрируем пользователя
        var registerRequest = new RegisterUserRequest { Login = login, Password = correctPassword };
        await _userService.RegisterAsync(registerRequest, ct);

        // Act & Assert
        var loginRequest = new LoginUserRequest { Login = login, Password = wrongPassword };
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.LoginAsync(loginRequest, ct));
    }

    /// <summary>
    /// Проверяет, что вход с несуществующим логином выбрасывает NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task LoginAsync_WithNonExistentLogin_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var loginRequest = new LoginUserRequest { Login = "nonexistent", Password = "password123" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.LoginAsync(loginRequest, ct));
    }

    /// <summary>
    /// Проверяет, что при попытке входа с пустым логином выбрасывается NotFoundException.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Scenario", "Failure")]
    public async Task LoginAsync_WithEmptyLogin_ThrowsNotFoundException(string? login)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var loginRequest = new LoginUserRequest
        {
            Login = login!,
            Password = "password123"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.LoginAsync(loginRequest, ct));
    }

    /// <summary>
    /// Проверяет, что при попытке входа с пустым паролем выбрасывается NotFoundException.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Scenario", "Failure")]
    public async Task LoginAsync_WithEmptyPassword_ThrowsNotFoundException(string? password)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var loginRequest = new LoginUserRequest
        {
            Login = "testuser",
            Password = password!
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.LoginAsync(loginRequest, ct));
    }

    /// <summary>
    /// Проверяет, что вход для пользователя с ролью Admin возвращает корректную роль.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task LoginAsync_WithAdminUser_ReturnsAdminRole()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var login = "adminuser";
        var password = "password123";
        var passwordHash = "hashed_password";
        var token = "jwt_token";

        _passwordHasherMock.Setup(p => p.Hash(password)).Returns(passwordHash);
        _passwordHasherMock.Setup(p => p.Verify(password, passwordHash)).Returns(true);
        _tokenGeneratorMock.Setup(t => t.CreateToken(It.IsAny<Guid>(), login, UserRole.Admin)).Returns(token);

        // Регистрируем администратора
        var registerRequest = new RegisterUserRequest
        {
            Login = login,
            Password = password,
            Role = "Admin"
        };
        await _userService.RegisterAsync(registerRequest, ct);

        // Act
        var loginRequest = new LoginUserRequest { Login = login, Password = password };
        var result = await _userService.LoginAsync(loginRequest, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Admin", result.Role);
    }

    /// <summary>
    /// Проверяет, что токен генерируется с корректными параметрами при входе.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task LoginAsync_GeneratesTokenWithCorrectParameters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var login = "testuser";
        var password = "password123";
        var passwordHash = "hashed_password";
        var token = "jwt_token";

        _passwordHasherMock.Setup(p => p.Hash(password)).Returns(passwordHash);
        _passwordHasherMock.Setup(p => p.Verify(password, passwordHash)).Returns(true);
        _tokenGeneratorMock.Setup(t => t.CreateToken(It.IsAny<Guid>(), login, UserRole.User)).Returns(token);

        // Регистрируем пользователя
        var registerRequest = new RegisterUserRequest { Login = login, Password = password };
        await _userService.RegisterAsync(registerRequest, ct);

        // Act
        var loginRequest = new LoginUserRequest { Login = login, Password = password };
        await _userService.LoginAsync(loginRequest, ct);

        // Assert
        _tokenGeneratorMock.Verify(
            t => t.CreateToken(It.IsAny<Guid>(), login, UserRole.User),
            Times.Once);
    }

    #endregion
}