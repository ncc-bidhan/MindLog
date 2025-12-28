using MindLog.Models;

namespace MindLog.Interfaces
{
    public interface IJournalEntryService
    {
        Task<List<JournalEntry>> GetEntriesByUserIdAsync(int userId);
        Task<PaginatedResult<JournalEntry>> GetEntriesByUserIdAsync(int userId, int pageNumber, int pageSize = 10);
        Task<JournalEntry?> GetEntryByIdAsync(int id, int userId);
        Task<JournalEntry?> GetEntryByDateAsync(DateOnly entryDate, int userId);
        Task<JournalEntry> CreateEntryAsync(JournalEntry entry, List<int>? moodIds = null);
        Task<JournalEntry> UpdateEntryAsync(JournalEntry entry, List<int>? moodIds = null);
        Task<bool> DeleteEntryAsync(int id, int userId);
        Task<bool> EntryExistsForDateAsync(DateOnly entryDate, int userId, int? excludeId = null);
        Task<List<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm);
        Task<PaginatedResult<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm, int pageNumber, int pageSize = 10);
        Task<List<JournalEntry>> SearchEntriesAdvancedAsync(int userId, SearchParameters searchParams);
        Task<PaginatedResult<JournalEntry>> SearchEntriesAdvancedAsync(int userId, SearchParameters searchParams, int pageNumber, int pageSize = 10);
        Task<List<JournalEntryMood>> GetEntryMoodsAsync(int entryId);
        Task<List<JournalEntry>> GetEntriesByMoodAsync(int userId, int moodId);
        Task<List<JournalEntry>> GetEntriesByMoodCategoryAsync(int userId, MoodCategory category);
        Task<List<Mood>> GetAllMoodsAsync();
        Task<List<JournalEntry>> GetEntriesByDateRangeAsync(int userId, DateOnly startDate, DateOnly endDate);
    }
}
