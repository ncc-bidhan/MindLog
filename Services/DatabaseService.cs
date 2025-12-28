using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindLog.Data;
using MindLog.Interfaces;
using MindLog.Helpers;
using System.Linq;

namespace MindLog.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<DatabaseService> _logger;
        private static readonly string[] RequiredTables = { "Users", "JournalEntries", "Moods", "JournalEntryMoods", "UserStreaks" };
        private static readonly string[] RequiredStreakColumns = { "CurrentStreak", "LongestStreak", "LastEntryDate" };

        public DatabaseService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = Logger.GetLogger<DatabaseService>();
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Initializing database...");
                
                await using var context = _contextFactory.CreateDbContext();
                await context.Database.EnsureCreatedAsync();
                _logger.LogInformation("Database created/ensured");
                
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                var tables = await GetExistingTablesAsync(connection);
                _logger.LogInformation("Tables found: {Tables}", string.Join(", ", tables));
                
                var missingTables = RequiredTables.Except(tables).ToList();
                
                if (tables.Contains("Users"))
                {
                    await EnsureStreakColumnsExistAsync(connection);
                }
                
                if (missingTables.Any())
                {
                    _logger.LogWarning("Missing tables: {MissingTables}", string.Join(", ", missingTables));
                    await connection.CloseAsync();
                    await RecreateDatabaseAsync();
                    return;
                }
                
                await connection.CloseAsync();
                _logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization error");
                throw;
            }
        }

        private async Task<List<string>> GetExistingTablesAsync(System.Data.Common.DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name IN ('{string.Join("', '", RequiredTables)}')";
            var reader = await command.ExecuteReaderAsync();
            
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        private async Task EnsureStreakColumnsExistAsync(System.Data.Common.DbConnection connection)
        {
            using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA table_info(Users)";
            var pragmaReader = await pragmaCommand.ExecuteReaderAsync();
            
            var columns = new List<string>();
            while (await pragmaReader.ReadAsync())
            {
                columns.Add(pragmaReader.GetString(1));
            }
            
            var missingColumns = RequiredStreakColumns.Except(columns).ToList();
            
            if (missingColumns.Any())
            {
                _logger.LogWarning("Missing streak columns in Users table: {MissingColumns}", string.Join(", ", missingColumns));
                
                using var alterCommand = connection.CreateCommand();
                foreach (var column in missingColumns)
                {
                    alterCommand.CommandText = column switch
                    {
                        "CurrentStreak" => "ALTER TABLE Users ADD COLUMN CurrentStreak INTEGER NOT NULL DEFAULT 0",
                        "LongestStreak" => "ALTER TABLE Users ADD COLUMN LongestStreak INTEGER NOT NULL DEFAULT 0", 
                        "LastEntryDate" => "ALTER TABLE Users ADD COLUMN LastEntryDate TEXT",
                        _ => null
                    };
                    
                    if (alterCommand.CommandText != null)
                    {
                        await alterCommand.ExecuteNonQueryAsync();
                        _logger.LogInformation("Added column {Column} to Users table", column);
                    }
                }
            }
        }

        private async Task RecreateDatabaseAsync()
        {
            _logger.LogInformation("Recreating database with updated schema...");
            await using var context = _contextFactory.CreateDbContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database recreated successfully");
        }

        public async Task ResetDatabaseAsync()
        {
            try
            {
                _logger.LogWarning("Resetting database...");
                await using var context = _contextFactory.CreateDbContext();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                _logger.LogInformation("Database reset completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting database");
                throw;
            }
        }
    }
}