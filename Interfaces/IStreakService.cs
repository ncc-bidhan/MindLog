using MindLog.Models;

namespace MindLog.Interfaces
{
    public interface IStreakService
    {
        Task<UserStreak> GetUserStreakAsync(int userId);
        Task<UserStreak> UpdateStreakAfterEntryAsync(int userId, DateOnly entryDate);
        Task<UserStreak> UpdateStreakAfterDeletionAsync(int userId);
        Task<List<DateOnly>> GetStreakCalendarAsync(int userId, int year, int month);
        Task<Dictionary<DateOnly, bool>> GetEntryStatusForMonthAsync(int userId, int year, int month);
    }
}
