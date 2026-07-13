using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Infrastructure.Persistence;
using Ya.Events.WebApi.IntegrationTests.Fixtures;

namespace Ya.Events.WebApi.IntegrationTests;

/// <summary>
/// Тесты для проверки корректности миграций БД.
/// Явно проверяют наличие таблиц, столбцов, типов данных, 
/// внешних ключей и ограничений.
/// </summary>
[Collection("PostgreSQL collection")]
public sealed class DatabaseMigrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public DatabaseMigrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    #region Table Existence Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTableExists()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tableExists = await TableExistsAsync(context, "events");

        // Assert
        Assert.True(tableExists, "Table 'events' should exist after migration");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTableExists()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tableExists = await TableExistsAsync(context, "bookings");

        // Assert
        Assert.True(tableExists, "Table 'bookings' should exist after migration");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTableExists()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tableExists = await TableExistsAsync(context, "users");

        // Assert
        Assert.True(tableExists, "Table 'users' should exist after migration");
    }

    #endregion

    #region Events Table Structure Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_HasRequiredColumns()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "events");
        var columnNames = columns.Select(c => c.ColumnName).ToHashSet();

        // Assert
        var requiredColumns = new[] { "id", "title", "description", "start_at", "end_at", "total_seats", "available_seats" };
        foreach (var column in requiredColumns)
        {
            Assert.Contains(column, columnNames);
        }
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_ColumnTypesAreCorrect()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "events");
        var columnMap = columns.ToDictionary(c => c.ColumnName, c => c.DataType);

        // Assert
        Assert.Equal("uuid", columnMap["id"]);
        Assert.Equal("character varying", columnMap["title"]);
        Assert.Equal("character varying", columnMap["description"]);
        Assert.Equal("timestamp with time zone", columnMap["start_at"]);
        Assert.Equal("timestamp with time zone", columnMap["end_at"]);
        Assert.Equal("integer", columnMap["total_seats"]);
        Assert.Equal("integer", columnMap["available_seats"]);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_HasPrimaryKey()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var primaryKey = await GetPrimaryKeyAsync(context, "events");

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Equal("PK_events", primaryKey);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_TitleColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "events");
        var titleColumn = columns.FirstOrDefault(c => c.ColumnName == "title");

        // Assert
        Assert.NotNull(titleColumn);
        Assert.False(titleColumn.IsNullable, "Column 'title' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_DescriptionColumnIsNullable()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "events");
        var descriptionColumn = columns.FirstOrDefault(c => c.ColumnName == "description");

        // Assert
        Assert.NotNull(descriptionColumn);
        Assert.True(descriptionColumn.IsNullable, "Column 'description' should be nullable");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_EventsTable_TitleHasMaxLength()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var titleMaxLength = await GetColumnMaxLengthAsync(context, "events", "title");

        // Assert
        Assert.Equal(200, titleMaxLength);
    }

    #endregion

    #region Bookings Table Structure Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasRequiredColumns()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "bookings");
        var columnNames = columns.Select(c => c.ColumnName).ToHashSet();

        // Assert
        var requiredColumns = new[] { "id", "event_id", "user_id", "status", "created_at", "processed_at" };
        foreach (var column in requiredColumns)
        {
            Assert.Contains(column, columnNames);
        }
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_ColumnTypesAreCorrect()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "bookings");
        var columnMap = columns.ToDictionary(c => c.ColumnName, c => c.DataType);

        // Assert
        Assert.Equal("uuid", columnMap["id"]);
        Assert.Equal("uuid", columnMap["event_id"]);
        Assert.Equal("uuid", columnMap["user_id"]);
        Assert.Equal("character varying", columnMap["status"]);
        Assert.Equal("timestamp with time zone", columnMap["created_at"]);
        Assert.Equal("timestamp with time zone", columnMap["processed_at"]);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasPrimaryKey()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var primaryKey = await GetPrimaryKeyAsync(context, "bookings");

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Equal("PK_bookings", primaryKey);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_EventIdColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "bookings");
        var eventIdColumn = columns.FirstOrDefault(c => c.ColumnName == "event_id");

        // Assert
        Assert.NotNull(eventIdColumn);
        Assert.False(eventIdColumn.IsNullable, "Column 'event_id' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_UserIdColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "bookings");
        var userIdColumn = columns.FirstOrDefault(c => c.ColumnName == "user_id");

        // Assert
        Assert.NotNull(userIdColumn);
        Assert.False(userIdColumn.IsNullable, "Column 'user_id' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_ProcessedAtIsNullable()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "bookings");
        var processedAtColumn = columns.FirstOrDefault(c => c.ColumnName == "processed_at");

        // Assert
        Assert.NotNull(processedAtColumn);
        Assert.True(processedAtColumn.IsNullable, "Column 'processed_at' should be nullable");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_StatusHasMaxLength()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var statusMaxLength = await GetColumnMaxLengthAsync(context, "bookings", "status");

        // Assert
        Assert.Equal(20, statusMaxLength);
    }

    #endregion

    #region Users Table Structure Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_HasRequiredColumns()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var columnNames = columns.Select(c => c.ColumnName).ToHashSet();

        // Assert
        var requiredColumns = new[] { "id", "login", "password_hash", "role" };
        foreach (var column in requiredColumns)
        {
            Assert.Contains(column, columnNames);
        }
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_ColumnTypesAreCorrect()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var columnMap = columns.ToDictionary(c => c.ColumnName, c => c.DataType);

        // Assert
        Assert.Equal("uuid", columnMap["id"]);
        Assert.Equal("character varying", columnMap["login"]);
        Assert.Equal("character varying", columnMap["password_hash"]);
        Assert.Equal("character varying", columnMap["role"]);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_HasPrimaryKey()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var primaryKey = await GetPrimaryKeyAsync(context, "users");

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Equal("PK_users", primaryKey);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_IdColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var idColumn = columns.FirstOrDefault(c => c.ColumnName == "id");

        // Assert
        Assert.NotNull(idColumn);
        Assert.False(idColumn.IsNullable, "Column 'id' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_LoginColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var loginColumn = columns.FirstOrDefault(c => c.ColumnName == "login");

        // Assert
        Assert.NotNull(loginColumn);
        Assert.False(loginColumn.IsNullable, "Column 'login' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_PasswordHashColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var passwordHashColumn = columns.FirstOrDefault(c => c.ColumnName == "password_hash");

        // Assert
        Assert.NotNull(passwordHashColumn);
        Assert.False(passwordHashColumn.IsNullable, "Column 'password_hash' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_RoleColumnNotNull()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await GetTableColumnsAsync(context, "users");
        var roleColumn = columns.FirstOrDefault(c => c.ColumnName == "role");

        // Assert
        Assert.NotNull(roleColumn);
        Assert.False(roleColumn.IsNullable, "Column 'role' should be NOT NULL");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_LoginHasMaxLength()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var loginMaxLength = await GetColumnMaxLengthAsync(context, "users", "login");

        // Assert
        Assert.Equal(256, loginMaxLength);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_PasswordHashHasMaxLength()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var passwordHashMaxLength = await GetColumnMaxLengthAsync(context, "users", "password_hash");

        // Assert
        Assert.Equal(512, passwordHashMaxLength);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_RoleHasMaxLength()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleMaxLength = await GetColumnMaxLengthAsync(context, "users", "role");

        // Assert
        Assert.Equal(20, roleMaxLength);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_HasUniqueConstraintOnLogin()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var uniqueConstraint = await GetUniqueConstraintAsync(context, "users", "login");

        // Assert
        Assert.NotNull(uniqueConstraint);
        Assert.Equal("IX_users_login", uniqueConstraint);
    }

    #endregion

    #region Foreign Key Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasForeignKeyToEvents()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var foreignKey = await GetForeignKeyAsync(context, "bookings", "event_id");

        // Assert
        Assert.NotNull(foreignKey);
        Assert.Equal("FK_bookings_events_event_id", foreignKey.ConstraintName);
        Assert.Equal("events", foreignKey.ReferencedTable);
        Assert.Equal("id", foreignKey.ReferencedColumn);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_ForeignKeyToEventHasCascadeDelete()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var foreignKey = await GetForeignKeyAsync(context, "bookings", "event_id");

        // Assert
        Assert.NotNull(foreignKey);
        Assert.Equal("CASCADE", foreignKey.DeleteRule);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasForeignKeyToUsers()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var foreignKey = await GetForeignKeyAsync(context, "bookings", "user_id");

        // Assert
        Assert.NotNull(foreignKey);
        Assert.Equal("FK_bookings_users_user_id", foreignKey.ConstraintName);
        Assert.Equal("users", foreignKey.ReferencedTable);
        Assert.Equal("id", foreignKey.ReferencedColumn);
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_ForeignKeyToUserHasCascadeDelete()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var foreignKey = await GetForeignKeyAsync(context, "bookings", "user_id");

        // Assert
        Assert.NotNull(foreignKey);
        Assert.Equal("CASCADE", foreignKey.DeleteRule);
    }

    #endregion

    #region Index Tests

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasIndexOnEventId()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var indexExists = await IndexExistsAsync(context, "bookings", "IX_bookings_event_id");

        // Assert
        Assert.True(indexExists, "Index 'IX_bookings_event_id' should exist on 'bookings' table");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_BookingsTable_HasIndexOnUserId()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var indexExists = await IndexExistsAsync(context, "bookings", "IX_bookings_user_id");

        // Assert
        Assert.True(indexExists, "Index 'IX_bookings_user_id' should exist on 'bookings' table");
    }

    [Fact]
    [Trait("Category", "DatabaseMigration")]
    public async Task Migration_UsersTable_HasIndexOnLogin()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var indexExists = await IndexExistsAsync(context, "users", "IX_users_login");

        // Assert
        Assert.True(indexExists, "Index 'IX_users_login' (unique constraint) should exist on 'users' table");
    }

    #endregion

    #region Helper Methods

    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        var result = await context.Database.SqlQueryRaw<string>(
            @"SELECT table_name as ""Value"" 
              FROM information_schema.tables 
              WHERE table_schema = 'public' AND table_name = @p0",
            tableName
        ).FirstOrDefaultAsync();

        return result != null;
    }

    private static async Task<List<ColumnInfo>> GetTableColumnsAsync(AppDbContext context, string tableName)
    {
        var columns = await context.Database.SqlQueryRaw<ColumnInfo>(
            @"SELECT 
                column_name as ""ColumnName"",
                data_type as ""DataType"",
                is_nullable = 'YES' as ""IsNullable"",
                character_maximum_length as ""MaxLength""
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @p0
            ORDER BY ordinal_position",
            tableName
        ).ToListAsync();

        return columns;
    }

    private static async Task<string?> GetPrimaryKeyAsync(AppDbContext context, string tableName)
    {
        var pk = await context.Database.SqlQueryRaw<string>(
            @"SELECT constraint_name as ""Value""
            FROM information_schema.table_constraints
            WHERE table_schema = 'public' 
                AND table_name = @p0 
                AND constraint_type = 'PRIMARY KEY'",
            tableName
        ).FirstOrDefaultAsync();

        return pk;
    }

    private static async Task<int?> GetColumnMaxLengthAsync(AppDbContext context, string tableName, string columnName)
    {
        var maxLength = await context.Database.SqlQueryRaw<int?>(
            @"SELECT character_maximum_length as ""Value""
            FROM information_schema.columns
            WHERE table_schema = 'public' 
                AND table_name = @p0 
                AND column_name = @p1",
            tableName,
            columnName
        ).FirstOrDefaultAsync();

        return maxLength;
    }

    private static async Task<string?> GetUniqueConstraintAsync(AppDbContext context, string tableName, string columnName)
    {
        var constraint = await context.Database.SqlQueryRaw<string>(
            @"SELECT indexname as ""Value""
            FROM pg_indexes
            WHERE schemaname = 'public' 
                AND tablename = @p0 
                AND indexname LIKE '%' || @p1 || '%'
                AND (indexdef LIKE '%UNIQUE%' OR indexdef LIKE '%unique%')",
            tableName,
            columnName
        ).FirstOrDefaultAsync();

        return constraint;
    }

    private static async Task<ForeignKeyInfo?> GetForeignKeyAsync(AppDbContext context, string tableName, string columnName)
    {
        var fk = await context.Database.SqlQueryRaw<ForeignKeyInfo>(
            @"SELECT
                tc.constraint_name as ""ConstraintName"",
                kcu.column_name as ""ColumnName"",
                ccu.table_name as ""ReferencedTable"",
                ccu.column_name as ""ReferencedColumn"",
                rc.delete_rule as ""DeleteRule""
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
            JOIN information_schema.referential_constraints rc ON rc.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
                AND tc.table_schema = 'public'
                AND tc.table_name = @p0
                AND kcu.column_name = @p1",
            tableName,
            columnName
        ).FirstOrDefaultAsync();

        return fk;
    }

    private static async Task<bool> IndexExistsAsync(AppDbContext context, string tableName, string indexName)
    {
        var result = await context.Database.SqlQueryRaw<string>(
            @"SELECT indexname as ""Value""
            FROM pg_indexes
            WHERE schemaname = 'public' 
                AND tablename = @p0 
                AND indexname = @p1",
            tableName,
            indexName
        ).FirstOrDefaultAsync();

        return result != null;
    }

    #endregion
}

/// <summary>
/// DTO для информации о столбце таблицы.
/// </summary>
public record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength
);

/// <summary>
/// DTO для информации о внешнем ключе.
/// </summary>
public record ForeignKeyInfo(
    string ConstraintName,
    string ColumnName,
    string ReferencedTable,
    string ReferencedColumn,
    string DeleteRule
);