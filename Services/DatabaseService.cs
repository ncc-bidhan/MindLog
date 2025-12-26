using Microsoft.EntityFrameworkCore;
using MindLog.Data;
using System.Linq;

namespace MindLog.Services
{
    public class DatabaseService
    {
        private readonly AppDbContext _context;

        public DatabaseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task InitializeDatabaseAsync()
    {
        try
        {
            Console.WriteLine("Initializing database...");
            
            // Ensure database exists with correct schema
            await _context.Database.EnsureCreatedAsync();
            Console.WriteLine("Database created/ensured");
            
            // Verify tables exist by trying to access them
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                // Check if all required tables exist
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Users', 'JournalEntries', 'Moods', 'JournalEntryMoods', 'UserStreaks')";
                var reader = await command.ExecuteReaderAsync();
                
                var tables = new List<string>();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
                
                Console.WriteLine($"Tables found: {string.Join(", ", tables)}");
                
                // Check if we need to update database schema
                var requiredTables = new[] { "Users", "JournalEntries", "Moods", "JournalEntryMoods", "UserStreaks" };
                var missingTables = requiredTables.Except(tables).ToList();
                
                // Check if Users table has the new streak columns
                bool needsStreakColumnsUpdate = false;
                if (!missingTables.Any() && tables.Contains("Users"))
                {
                    using var pragmaCommand = connection.CreateCommand();
                    pragmaCommand.CommandText = "PRAGMA table_info(Users)";
                    var pragmaReader = await pragmaCommand.ExecuteReaderAsync();
                    
                    var columns = new List<string>();
                    while (await pragmaReader.ReadAsync())
                    {
                        columns.Add(pragmaReader.GetString(1)); // Column name is at index 1
                    }
                    
                    var requiredColumns = new[] { "CurrentStreak", "LongestStreak", "LastEntryDate" };
                    var missingColumns = requiredColumns.Except(columns).ToList();
                    
                    if (missingColumns.Any())
                    {
                        Console.WriteLine($"Missing streak columns in Users table: {string.Join(", ", missingColumns)}");
                        needsStreakColumnsUpdate = true;
                        
                        // Add missing columns
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
                                Console.WriteLine($"Added column {column} to Users table");
                            }
                        }
                    }
                }
                
                // Only recreate if tables are missing
                bool needsRecreation = missingTables.Any();
                
                if (needsRecreation)
                {
                    if (missingTables.Any())
                    {
                        Console.WriteLine($"Missing tables: {string.Join(", ", missingTables)}");
                    }
                    else
                    {
                        Console.WriteLine("Database schema update required (mood constraint changes)");
                    }
                    Console.WriteLine("Recreating database with updated schema...");
                    
                    await connection.CloseAsync();
                    
                    // Drop existing database and recreate with all tables
                    await _context.Database.EnsureDeletedAsync();
                    await _context.Database.EnsureCreatedAsync();
                    
                    Console.WriteLine("Database recreated successfully");
                    return;
                }
                
                await connection.CloseAsync();
                Console.WriteLine("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying tables: {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization error: {ex.Message}");
            throw;
        }
    }

        public async Task ResetDatabaseAsync()
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();
        }
    }
}