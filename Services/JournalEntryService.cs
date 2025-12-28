using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindLog.Data;
using MindLog.Exceptions;
using MindLog.Helpers;
using MindLog.Interfaces;
using MindLog.Models;

namespace MindLog.Services
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<JournalEntryService> _logger;

        public JournalEntryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = Logger.GetLogger<JournalEntryService>();
        }

        private void ValidateEntry(JournalEntry entry)
        {
            if (entry.UserId <= 0)
            {
                throw new ValidationException("User ID is required.");
            }

            if (entry.EntryDate == default)
            {
                throw new ValidationException("Entry date is required.");
            }

            if (entry.EntryDate > DateOnly.FromDateTime(DateTime.Now))
            {
                throw new ValidationException("Entry date cannot be in the future");
            }

            if (entry.EntryDate < DateOnly.FromDateTime(DateTime.Now.AddYears(-10)))
            {
                throw new ValidationException("Entry date cannot be more than 10 years in the past");
            }
        }



        public async Task<List<JournalEntry>> GetEntriesByUserIdAsync(int userId)
        {
            await using var context = _contextFactory.CreateDbContext();
            return await context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<PaginatedResult<JournalEntry>> GetEntriesByUserIdAsync(int userId, int pageNumber, int pageSize = 10)
        {
            await using var context = _contextFactory.CreateDbContext();
            var query = context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<JournalEntry>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<JournalEntry?> GetEntryByIdAsync(int id, int userId)
        {
            await using var context = _contextFactory.CreateDbContext();
            return await context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        }

        public async Task<JournalEntry?> GetEntryByDateAsync(DateOnly entryDate, int userId)
        {
            await using var context = _contextFactory.CreateDbContext();
            return await context.JournalEntries
                .FirstOrDefaultAsync(e => e.EntryDate == entryDate && e.UserId == userId);
        }

        public async Task<JournalEntry> CreateEntryAsync(JournalEntry entry, List<int>? moodIds = null)
        {
            try
            {
                ValidateEntry(entry);

                await using var context = _contextFactory.CreateDbContext();

                if (await EntryExistsForDateAsync(context, entry.EntryDate, entry.UserId))
                {
                    throw new DuplicateEntryException($"An entry already exists for {entry.EntryDate:MMMM d, yyyy}. Only one entry is allowed per day.");
                }

                entry.CreatedAt = DateTime.Now;
                entry.UpdatedAt = DateTime.Now;

                context.JournalEntries.Add(entry);
                await context.SaveChangesAsync();

                if (moodIds != null && moodIds.Any())
                {
                    ValidateMoods(context, moodIds);
                    await AddMoodsToEntryAsync(context, entry.Id, moodIds);
                }

                _logger.LogInformation("Created journal entry for UserId: {UserId}, Date: {EntryDate}", entry.UserId, entry.EntryDate);
                return entry;
            }
            catch (DuplicateEntryException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry for UserId: {UserId}", entry.UserId);
                throw;
            }
        }

        public async Task<JournalEntry> UpdateEntryAsync(JournalEntry entry, List<int>? moodIds = null)
        {
            try
            {
                ValidateEntry(entry);

                await using var context = _contextFactory.CreateDbContext();

                if (await EntryExistsForDateAsync(context, entry.EntryDate, entry.UserId, entry.Id))
                {
                    throw new DuplicateEntryException($"An entry already exists for {entry.EntryDate:MMMM d, yyyy}. Only one entry is allowed per day.");
                }

                entry.UpdatedAt = DateTime.Now;

                context.JournalEntries.Update(entry);
                await context.SaveChangesAsync();

                if (moodIds != null)
                {
                    ValidateMoods(context, moodIds);
                    await UpdateEntryMoodsAsync(context, entry.Id, moodIds);
                }

                _logger.LogInformation("Updated journal entry Id: {EntryId}, UserId: {UserId}", entry.Id, entry.UserId);
                return entry;
            }
            catch (DuplicateEntryException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal entry Id: {EntryId}, UserId: {UserId}", entry.Id, entry.UserId);
                throw;
            }
        }

        public async Task<bool> DeleteEntryAsync(int id, int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var entry = await context.JournalEntries
                    .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

                if (entry == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent entry Id: {EntryId}, UserId: {UserId}", id, userId);
                    return false;
                }

                context.JournalEntries.Remove(entry);
                await context.SaveChangesAsync();

                _logger.LogInformation("Deleted journal entry Id: {EntryId}, UserId: {UserId}", id, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting journal entry Id: {EntryId}, UserId: {UserId}", id, userId);
                throw;
            }
        }

        public async Task<bool> EntryExistsForDateAsync(DateOnly entryDate, int userId, int? excludeId = null)
        {
            await using var context = _contextFactory.CreateDbContext();
            var query = context.JournalEntries
                .Where(e => e.EntryDate == entryDate && e.UserId == userId);

            if (excludeId.HasValue)
                query = query.Where(e => e.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        private async Task<bool> EntryExistsForDateAsync(AppDbContext context, DateOnly entryDate, int userId, int? excludeId = null)
        {
            var query = context.JournalEntries
                .Where(e => e.EntryDate == entryDate && e.UserId == userId);

            if (excludeId.HasValue)
                query = query.Where(e => e.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<List<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.JournalEntries
                    .Where(e => e.UserId == userId &&
                               (e.Title.Contains(searchTerm) ||
                                e.Content.Contains(searchTerm) ||
                                (e.Tags != null && e.Tags.Contains(searchTerm))))
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching entries for UserId: {UserId}, Term: {SearchTerm}", userId, searchTerm);
                throw;
            }
        }

        public async Task<PaginatedResult<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm, int pageNumber, int pageSize = 10)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                var query = context.JournalEntries
                    .Where(e => e.UserId == userId &&
                               (e.Title.Contains(searchTerm) ||
                                e.Content.Contains(searchTerm) ||
                                (e.Tags != null && e.Tags.Contains(searchTerm))))
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt);

                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PaginatedResult<JournalEntry>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching entries for UserId: {UserId}, Term: {SearchTerm}, Page: {PageNumber}", userId, searchTerm, pageNumber);
                throw;
            }
        }

        public async Task<List<JournalEntry>> SearchEntriesAdvancedAsync(int userId, SearchParameters searchParams)
        {
            await using var context = _contextFactory.CreateDbContext();
            var query = context.JournalEntries.Where(e => e.UserId == userId);

            // Text search
            if (!string.IsNullOrWhiteSpace(searchParams.SearchTerm))
            {
                query = query.Where(e =>
                    e.Title.Contains(searchParams.SearchTerm) ||
                    e.Content.Contains(searchParams.SearchTerm) ||
                    (e.Tags != null && e.Tags.Contains(searchParams.SearchTerm)));
            }

            // Date range filter
            if (searchParams.StartDate.HasValue)
            {
                query = query.Where(e => e.EntryDate >= searchParams.StartDate.Value);
            }

            if (searchParams.EndDate.HasValue)
            {
                query = query.Where(e => e.EntryDate <= searchParams.EndDate.Value);
            }

            // Mood filter
            if (searchParams.MoodIds != null && searchParams.MoodIds.Any())
            {
                query = query.Where(e => e.JournalEntryMoods
                    .Any(jem => searchParams.MoodIds.Contains(jem.MoodId)));
            }

            // Tags filter
            if (searchParams.Tags != null && searchParams.Tags.Any())
            {
                foreach (var tag in searchParams.Tags)
                {
                    query = query.Where(e => e.Tags != null && e.Tags.Contains(tag));
                }
            }

            return await query
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<PaginatedResult<JournalEntry>> SearchEntriesAdvancedAsync(int userId, SearchParameters searchParams, int pageNumber, int pageSize = 10)
        {
            await using var context = _contextFactory.CreateDbContext();
            var query = context.JournalEntries.Where(e => e.UserId == userId);

            // Text search
            if (!string.IsNullOrWhiteSpace(searchParams.SearchTerm))
            {
                query = query.Where(e =>
                    e.Title.Contains(searchParams.SearchTerm) ||
                    e.Content.Contains(searchParams.SearchTerm) ||
                    (e.Tags != null && e.Tags.Contains(searchParams.SearchTerm)));
            }

            // Date range filter
            if (searchParams.StartDate.HasValue)
            {
                query = query.Where(e => e.EntryDate >= searchParams.StartDate.Value);
            }

            if (searchParams.EndDate.HasValue)
            {
                query = query.Where(e => e.EntryDate <= searchParams.EndDate.Value);
            }

            // Mood filter
            if (searchParams.MoodIds != null && searchParams.MoodIds.Any())
            {
                query = query.Where(e => e.JournalEntryMoods
                    .Any(jem => searchParams.MoodIds.Contains(jem.MoodId)));
            }

            // Tags filter
            if (searchParams.Tags != null && searchParams.Tags.Any())
            {
                foreach (var tag in searchParams.Tags)
                {
                    query = query.Where(e => e.Tags != null && e.Tags.Contains(tag));
                }
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<JournalEntry>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }



        private void ValidateMoods(AppDbContext context, List<int>? moodIds)
        {
            if (moodIds == null || !moodIds.Any())
            {
                throw new ValidationException("At least one mood must be selected.");
            }

            if (moodIds.Count > 3)
            {
                throw new ValidationException("Maximum of 3 moods allowed (1 primary + 2 secondary).");
            }

            var existingMoodIds = context.Moods
                .Where(m => moodIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToList();

            var invalidMoodIds = moodIds.Except(existingMoodIds).ToList();
            if (invalidMoodIds.Any())
            {
                _logger.LogWarning("Invalid mood IDs provided: {MoodIds}", string.Join(", ", invalidMoodIds));
                throw new ValidationException($"Invalid mood IDs provided: {string.Join(", ", invalidMoodIds)}");
            }
        }

        private async Task AddMoodsToEntryAsync(AppDbContext context, int entryId, List<int> moodIds)
        {
            var entryMoods = new List<JournalEntryMood>();

            for (int i = 0; i < moodIds.Count; i++)
            {
                entryMoods.Add(new JournalEntryMood
                {
                    JournalEntryId = entryId,
                    MoodId = moodIds[i],
                    IsPrimary = i == 0
                });
            }

            context.JournalEntryMoods.AddRange(entryMoods);
            await context.SaveChangesAsync();

            _logger.LogInformation("Added {Count} moods to entry Id: {EntryId}", entryMoods.Count, entryId);
        }

        private async Task UpdateEntryMoodsAsync(AppDbContext context, int entryId, List<int> moodIds)
        {
            var existingMoods = await context.JournalEntryMoods
                .Where(jem => jem.JournalEntryId == entryId)
                .ToListAsync();

            context.JournalEntryMoods.RemoveRange(existingMoods);
            await context.SaveChangesAsync();

            await AddMoodsToEntryAsync(context, entryId, moodIds);
        }

        public async Task<List<JournalEntryMood>> GetEntryMoodsAsync(int entryId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.JournalEntryMoods
                    .Include(jem => jem.Mood)
                    .Where(jem => jem.JournalEntryId == entryId)
                    .OrderBy(jem => jem.IsPrimary ? 0 : 1)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving moods for entry Id: {EntryId}", entryId);
                throw;
            }
        }

        public async Task<List<JournalEntry>> GetEntriesByMoodAsync(int userId, int moodId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.JournalEntryMoods
                    .Where(jem => jem.JournalEntry.UserId == userId && jem.MoodId == moodId)
                    .Select(jem => jem.JournalEntry)
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entries for UserId: {UserId}, MoodId: {MoodId}", userId, moodId);
                throw;
            }
        }

        public async Task<List<JournalEntry>> GetEntriesByMoodCategoryAsync(int userId, MoodCategory category)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.JournalEntryMoods
                    .Where(jem => jem.JournalEntry.UserId == userId && jem.Mood.Category == category)
                    .Select(jem => jem.JournalEntry)
                    .Distinct()
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entries for UserId: {UserId}, MoodCategory: {Category}", userId, category);
                throw;
            }
        }

        public async Task<List<Mood>> GetAllMoodsAsync()
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.Moods
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Category)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all moods");
                throw;
            }
        }

        public async Task<List<JournalEntry>> GetEntriesByDateRangeAsync(int userId, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.JournalEntries
                    .Where(e => e.UserId == userId &&
                               e.EntryDate >= startDate &&
                               e.EntryDate <= endDate)
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entries for UserId: {UserId}, DateRange: {StartDate} to {EndDate}", userId, startDate, endDate);
                throw;
            }
        }
    }
}
