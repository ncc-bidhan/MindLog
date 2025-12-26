using Microsoft.EntityFrameworkCore;
using MindLog.Data;
using MindLog.Models;

namespace MindLog.Services
{
    public class StreakService
    {
        private readonly AppDbContext _context;
        private readonly JournalEntryService _journalEntryService;

        public StreakService(AppDbContext context, JournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task<UserStreak> GetUserStreakAsync(int userId)
        {
            var userStreak = await _context.UserStreaks
                .FirstOrDefaultAsync(us => us.UserId == userId);

            if (userStreak == null)
            {
                userStreak = await InitializeUserStreakAsync(userId);
            }

            return userStreak;
        }

        public async Task<UserStreak> UpdateStreakAfterEntryAsync(int userId, DateOnly entryDate)
        {
            var userStreak = await GetUserStreakAsync(userId);
            var allEntries = await _journalEntryService.GetEntriesByUserIdAsync(userId);
            
            await CalculateAndUpdateStreak(userStreak, allEntries);
            
            return userStreak;
        }

        public async Task<UserStreak> UpdateStreakAfterDeletionAsync(int userId)
        {
            var userStreak = await GetUserStreakAsync(userId);
            var allEntries = await _journalEntryService.GetEntriesByUserIdAsync(userId);
            
            await CalculateAndUpdateStreak(userStreak, allEntries);
            
            return userStreak;
        }

        private async Task<UserStreak> InitializeUserStreakAsync(int userId)
        {
            var userStreak = new UserStreak
            {
                UserId = userId,
                CurrentStreak = 0,
                LongestStreak = 0,
                TotalEntries = 0,
                MissedDays = 0,
                StreakStartDate = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.UserStreaks.Add(userStreak);
            await _context.SaveChangesAsync();

            return userStreak;
        }

        private async Task CalculateAndUpdateStreak(UserStreak userStreak, List<JournalEntry> allEntries)
        {
            if (!allEntries.Any())
            {
                userStreak.CurrentStreak = 0;
                userStreak.LongestStreak = 0;
                userStreak.TotalEntries = 0;
                userStreak.MissedDays = 0;
                userStreak.LastEntryDate = null;
                userStreak.StreakStartDate = DateTime.Now;
            }
            else
            {
                var entryDates = allEntries
                    .Select(e => e.EntryDate)
                    .OrderBy(d => d)
                    .ToList();

                userStreak.TotalEntries = entryDates.Count;
                userStreak.LastEntryDate = entryDates.Max().ToDateTime(TimeOnly.MinValue);

                var (currentStreak, longestStreak, missedDays, streakStartDate) = 
                    CalculateStreakMetrics(entryDates);

                userStreak.CurrentStreak = currentStreak;
                userStreak.LongestStreak = longestStreak;
                userStreak.MissedDays = missedDays;
                userStreak.StreakStartDate = streakStartDate.ToDateTime(TimeOnly.MinValue);
            }

            userStreak.UpdatedAt = DateTime.Now;
            _context.UserStreaks.Update(userStreak);
            await _context.SaveChangesAsync();
        }

        private (int currentStreak, int longestStreak, int missedDays, DateOnly streakStartDate) 
            CalculateStreakMetrics(List<DateOnly> entryDates)
        {
            if (!entryDates.Any())
                return (0, 0, 0, DateOnly.FromDateTime(DateTime.Now));

            var sortedDates = entryDates.Distinct().OrderBy(d => d).ToList();
            var today = DateOnly.FromDateTime(DateTime.Now);
            
            // Calculate current streak
            int currentStreak = 0;
            DateOnly streakStartDate = sortedDates.First();
            
            // Check if the most recent entry is today or yesterday to maintain current streak
            var mostRecentEntry = sortedDates.Last();
            
            if (mostRecentEntry == today || mostRecentEntry == today.AddDays(-1))
            {
                // Start from the most recent entry and work backwards
                currentStreak = 1;
                var currentDate = mostRecentEntry;
                
                for (int i = sortedDates.Count - 2; i >= 0; i--)
                {
                    var expectedDate = currentDate.AddDays(-1);
                    if (sortedDates[i] == expectedDate)
                    {
                        currentStreak++;
                        currentDate = expectedDate;
                        streakStartDate = currentDate;
                    }
                    else if (sortedDates[i] < expectedDate)
                    {
                        // Break in streak found
                        break;
                    }
                }
            }
            else if (mostRecentEntry < today.AddDays(-1))
            {
                // Streak is broken
                currentStreak = 0;
                
                // Find the start of the most recent streak
                for (int i = sortedDates.Count - 1; i >= 0; i--)
                {
                    if (i == 0)
                    {
                        streakStartDate = sortedDates[i];
                        break;
                    }
                    
                    var expectedDate = sortedDates[i].AddDays(-1);
                    if (sortedDates[i - 1] == expectedDate)
                    {
                        continue;
                    }
                    else
                    {
                        streakStartDate = sortedDates[i];
                        break;
                    }
                }
            }

            // Calculate longest streak
            int longestStreak = 0;
            int tempStreak = 1;
            
            for (int i = 1; i < sortedDates.Count; i++)
            {
                var expectedDate = sortedDates[i - 1].AddDays(1);
                if (sortedDates[i] == expectedDate)
                {
                    tempStreak++;
                }
                else
                {
                    longestStreak = Math.Max(longestStreak, tempStreak);
                    tempStreak = 1;
                }
            }
            longestStreak = Math.Max(longestStreak, tempStreak);

            // Calculate missed days since first entry
            int missedDays = 0;
            if (sortedDates.Any())
            {
                var firstEntry = sortedDates.First();
                var daysSinceFirstEntry = today.DayNumber - firstEntry.DayNumber + 1;
                missedDays = Math.Max(0, daysSinceFirstEntry - sortedDates.Count);
            }

            return (currentStreak, longestStreak, missedDays, streakStartDate);
        }

        public async Task<List<DateOnly>> GetStreakCalendarAsync(int userId, int year, int month)
        {
            var entries = await _journalEntryService.GetEntriesByUserIdAsync(userId);
            var startDate = new DateOnly(year, month, 1);
            var endDate = month == 12 
                ? new DateOnly(year + 1, 1, 1).AddDays(-1)
                : new DateOnly(year, month + 1, 1).AddDays(-1);

            return entries
                .Where(e => e.EntryDate >= startDate && e.EntryDate <= endDate)
                .Select(e => e.EntryDate)
                .ToList();
        }

        public async Task<Dictionary<DateOnly, bool>> GetEntryStatusForMonthAsync(int userId, int year, int month)
        {
            var entryDates = await GetStreakCalendarAsync(userId, year, month);
            var startDate = new DateOnly(year, month, 1);
            var endDate = month == 12 
                ? new DateOnly(year + 1, 1, 1).AddDays(-1)
                : new DateOnly(year, month + 1, 1).AddDays(-1);

            var result = new Dictionary<DateOnly, bool>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                result[date] = entryDates.Contains(date);
            }

            return result;
        }
    }
}