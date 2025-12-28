using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindLog.Data;
using MindLog.Helpers;
using MindLog.Interfaces;
using MindLog.Models;

namespace MindLog.Services
{
    public class StreakService : IStreakService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<StreakService> _logger;

        public StreakService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = Logger.GetLogger<StreakService>();
        }

        public async Task<UserStreak> GetUserStreakAsync(int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var userStreak = await context.UserStreaks
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userStreak == null)
                {
                    userStreak = await InitializeUserStreakAsync(context, userId);
                }

                return userStreak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user streak for UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserStreak> UpdateStreakAfterEntryAsync(int userId, DateOnly entryDate)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var userStreak = await GetUserStreakAsync(userId);
                var allEntries = await context.JournalEntries
                    .Where(e => e.UserId == userId)
                    .OrderBy(e => e.EntryDate)
                    .Select(e => e.EntryDate)
                    .ToListAsync();

                await CalculateAndUpdateStreak(context, userStreak, allEntries);

                _logger.LogInformation("Updated streak after entry for UserId: {UserId}, Date: {EntryDate}", userId, entryDate);
                return userStreak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating streak after entry for UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserStreak> UpdateStreakAfterDeletionAsync(int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var userStreak = await GetUserStreakAsync(userId);
                var allEntries = await context.JournalEntries
                    .Where(e => e.UserId == userId)
                    .OrderBy(e => e.EntryDate)
                    .Select(e => e.EntryDate)
                    .ToListAsync();

                await CalculateAndUpdateStreak(context, userStreak, allEntries);

                _logger.LogInformation("Updated streak after deletion for UserId: {UserId}", userId);
                return userStreak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating streak after deletion for UserId: {UserId}", userId);
                throw;
            }
        }

        private async Task<UserStreak> InitializeUserStreakAsync(AppDbContext context, int userId)
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

            context.UserStreaks.Add(userStreak);
            await context.SaveChangesAsync();

            _logger.LogInformation("Initialized user streak for UserId: {UserId}", userId);
            return userStreak;
        }

        private async Task CalculateAndUpdateStreak(AppDbContext context, UserStreak userStreak, List<DateOnly> allEntryDates)
        {
            if (!allEntryDates.Any())
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
                userStreak.TotalEntries = allEntryDates.Count;
                userStreak.LastEntryDate = allEntryDates.Max().ToDateTime(TimeOnly.MinValue);

                var (currentStreak, longestStreak, missedDays, streakStartDate) =
                    CalculateStreakMetrics(allEntryDates);

                userStreak.CurrentStreak = currentStreak;
                userStreak.LongestStreak = longestStreak;
                userStreak.MissedDays = missedDays;
                userStreak.StreakStartDate = streakStartDate.ToDateTime(TimeOnly.MinValue);
            }

            userStreak.UpdatedAt = DateTime.Now;
            context.UserStreaks.Update(userStreak);
            await context.SaveChangesAsync();
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
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var startDate = new DateOnly(year, month, 1);
                var endDate = month == 12 
                    ? new DateOnly(year + 1, 1, 1).AddDays(-1)
                    : new DateOnly(year, month + 1, 1).AddDays(-1);

                var entries = await context.JournalEntries
                    .Where(e => e.UserId == userId && e.EntryDate >= startDate && e.EntryDate <= endDate)
                    .Select(e => e.EntryDate)
                    .ToListAsync();

                _logger.LogInformation("Retrieved streak calendar for UserId: {UserId}, Month: {Month}/{Year}", userId, month, year);
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving streak calendar for UserId: {UserId}, Month: {Month}/{Year}", userId, month, year);
                throw;
            }
        }

        public async Task<Dictionary<DateOnly, bool>> GetEntryStatusForMonthAsync(int userId, int year, int month)
        {
            try
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

                _logger.LogInformation("Retrieved entry status for UserId: {UserId}, Month: {Month}/{Year}", userId, month, year);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entry status for UserId: {UserId}, Month: {Month}/{Year}", userId, month, year);
                throw;
            }
        }
    }
}
