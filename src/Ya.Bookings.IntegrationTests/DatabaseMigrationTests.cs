using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ya.Bookings.IntegrationTests.Fixtures;
using Ya.Bookings.Infrastructure.Persistence;

namespace Ya.Bookings.IntegrationTests;

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
    public async Task Migration_BookingsTableExists()
    {
        // Arrange & Act
        using var scope = _fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tableExists = await TableExistsAsync(context, "bookings");

        // Assert
        Assert.True(tableExists, "Table 'bookings' should exist after migration");
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