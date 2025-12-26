using Microsoft.EntityFrameworkCore;
using MindLog.Data;
using MindLog.Models;
using System.ComponentModel.DataAnnotations;

namespace MindLog.Services
{
    public class JournalEntryService
    {
        private readonly AppDbContext _context;

        public JournalEntryService(AppDbContext context)
        {
            _context = context;
        }

        private void ValidateEntry(JournalEntry entry)
        {
            var validationContext = new ValidationContext(entry);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(entry, validationContext, validationResults, true))
            {
                var errorMessages = validationResults
                    .Select(vr => vr.ErrorMessage)
                    .Where(em => !string.IsNullOrEmpty(em))
                    .ToList();
                
                throw new ValidationException(string.Join("; ", errorMessages));
            }

            // Additional business logic validation
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
            return await _context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<PaginatedResult<JournalEntry>> GetEntriesByUserIdAsync(int userId, int pageNumber, int pageSize = 10)
        {
            var query = _context.JournalEntries
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
            return await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        }

        public async Task<JournalEntry?> GetEntryByDateAsync(DateOnly entryDate, int userId)
        {
            return await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.EntryDate == entryDate && e.UserId == userId);
        }

        public async Task<JournalEntry> CreateEntryAsync(JournalEntry entry, List<int>? moodIds = null)
        {
            ValidateEntry(entry);

            // Check for one-entry-per-day constraint
            var existingEntry = await EntryExistsForDateAsync(entry.EntryDate, entry.UserId);
            if (existingEntry)
            {
                throw new InvalidOperationException($"An entry already exists for {entry.EntryDate:MMMM d, yyyy}. Only one entry is allowed per day.");
            }

            entry.CreatedAt = DateTime.Now;
            entry.UpdatedAt = DateTime.Now;
            
            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();

            // Add moods if provided
            if (moodIds != null && moodIds.Any())
            {
                ValidateMoods(moodIds);
                await AddMoodsToEntryAsync(entry.Id, moodIds);
            }

            // Update user streak after creating entry
            await UpdateUserStreakAsync(entry.UserId);

            return entry;
        }

        public async Task<JournalEntry> UpdateEntryAsync(JournalEntry entry, List<int>? moodIds = null)
        {
            ValidateEntry(entry);

            // Check for one-entry-per-day constraint (excluding current entry)
            var existingEntry = await EntryExistsForDateAsync(entry.EntryDate, entry.UserId, entry.Id);
            if (existingEntry)
            {
                throw new InvalidOperationException($"An entry already exists for {entry.EntryDate:MMMM d, yyyy}. Only one entry is allowed per day.");
            }

            entry.UpdatedAt = DateTime.Now;
            
            _context.JournalEntries.Update(entry);
            await _context.SaveChangesAsync();

            // Update moods if provided
            if (moodIds != null)
            {
                ValidateMoods(moodIds);
                await UpdateEntryMoodsAsync(entry.Id, moodIds);
            }

            return entry;
        }

        public async Task<bool> DeleteEntryAsync(int id, int userId)
        {
            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            
            if (entry == null)
                return false;

            _context.JournalEntries.Remove(entry);
            await _context.SaveChangesAsync();
            
            // Update user streak after deleting entry
            await UpdateUserStreakAsync(userId);
            
            return true;
        }

        public async Task<bool> EntryExistsForDateAsync(DateOnly entryDate, int userId, int? excludeId = null)
        {
            var query = _context.JournalEntries
                .Where(e => e.EntryDate == entryDate && e.UserId == userId);
            
            if (excludeId.HasValue)
                query = query.Where(e => e.Id != excludeId.Value);
            
            return await query.AnyAsync();
        }

        public async Task<List<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm)
        {
            return await _context.JournalEntries
                .Where(e => e.UserId == userId && 
                           (e.Title.Contains(searchTerm) || 
                            e.Content.Contains(searchTerm) ||
                            (e.Tags != null && e.Tags.Contains(searchTerm))))
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<PaginatedResult<JournalEntry>> SearchEntriesAsync(int userId, string searchTerm, int pageNumber, int pageSize = 10)
        {
            var query = _context.JournalEntries
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

        public async Task<List<JournalEntry>> SearchEntriesAdvancedAsync(int userId, SearchParameters searchParams)
        {
            var query = _context.JournalEntries.Where(e => e.UserId == userId);

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
            var query = _context.JournalEntries.Where(e => e.UserId == userId);

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



        private void ValidateMoods(List<int>? moodIds)
        {
            Console.WriteLine($"ValidateMoods called with: [{string.Join(", ", moodIds ?? new List<int>())}]");
            
            if (moodIds == null || !moodIds.Any())
            {
                throw new ValidationException("At least one mood must be selected.");
            }

            if (moodIds.Count > 3)
            {
                throw new ValidationException("Maximum of 3 moods allowed (1 primary + 2 secondary).");
            }

            // Check if all mood IDs exist
            var totalMoodsInDb = _context.Moods.Count();
            Console.WriteLine($"Total moods in database: {totalMoodsInDb}");
            
            var existingMoodIds = _context.Moods
                .Where(m => moodIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToList();

            Console.WriteLine($"Mood IDs found in database: [{string.Join(", ", existingMoodIds)}]");

            var invalidMoodIds = moodIds.Except(existingMoodIds).ToList();
            if (invalidMoodIds.Any())
            {
                Console.WriteLine($"Invalid mood IDs: [{string.Join(", ", invalidMoodIds)}]");
                throw new ValidationException($"Invalid mood IDs provided: {string.Join(", ", invalidMoodIds)}");
            }
        }

        private async Task AddMoodsToEntryAsync(int entryId, List<int> moodIds)
        {
            Console.WriteLine($"AddMoodsToEntryAsync: entryId={entryId}, moodIds=[{string.Join(", ", moodIds)}]");
            
            var entryMoods = new List<JournalEntryMood>();
            
            for (int i = 0; i < moodIds.Count; i++)
            {
                var entryMood = new JournalEntryMood
                {
                    JournalEntryId = entryId,
                    MoodId = moodIds[i],
                    IsPrimary = i == 0 // First mood is primary
                };
                entryMoods.Add(entryMood);
                Console.WriteLine($"Created JournalEntryMood: JournalEntryId={entryMood.JournalEntryId}, MoodId={entryMood.MoodId}, IsPrimary={entryMood.IsPrimary}");
            }

            _context.JournalEntryMoods.AddRange(entryMoods);
            Console.WriteLine($"Adding {entryMoods.Count} JournalEntryMood records to database");
            await _context.SaveChangesAsync();
            Console.WriteLine("Successfully saved JournalEntryMoods");
        }

        private async Task UpdateEntryMoodsAsync(int entryId, List<int> moodIds)
        {
            Console.WriteLine($"UpdateEntryMoodsAsync: entryId={entryId}, moodIds=[{string.Join(", ", moodIds)}]");
            
            // Remove existing moods
            var existingMoods = await _context.JournalEntryMoods
                .Where(jem => jem.JournalEntryId == entryId)
                .ToListAsync();

            Console.WriteLine($"Found {existingMoods.Count} existing JournalEntryMoods to remove");
            _context.JournalEntryMoods.RemoveRange(existingMoods);
            await _context.SaveChangesAsync();
            Console.WriteLine("Successfully removed existing moods");

            // Add new moods
            await AddMoodsToEntryAsync(entryId, moodIds);
        }

        public async Task<List<JournalEntryMood>> GetEntryMoodsAsync(int entryId)
        {
            return await _context.JournalEntryMoods
                .Include(jem => jem.Mood)
                .Where(jem => jem.JournalEntryId == entryId)
                .OrderBy(jem => jem.IsPrimary ? 0 : 1) // Primary mood first
                .ToListAsync();
        }

        public async Task<List<JournalEntry>> GetEntriesByMoodAsync(int userId, int moodId)
        {
            return await _context.JournalEntryMoods
                .Where(jem => jem.JournalEntry.UserId == userId && jem.MoodId == moodId)
                .Select(jem => jem.JournalEntry)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<JournalEntry>> GetEntriesByMoodCategoryAsync(int userId, MoodCategory category)
        {
            return await _context.JournalEntryMoods
                .Where(jem => jem.JournalEntry.UserId == userId && jem.Mood.Category == category)
                .Select(jem => jem.JournalEntry)
                .Distinct()
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Mood>> GetAllMoodsAsync()
        {
            return await _context.Moods
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<List<JournalEntry>> GetEntriesByDateRangeAsync(int userId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.JournalEntries
                .Where(e => e.UserId == userId && 
                           e.EntryDate >= startDate && 
                           e.EntryDate <= endDate)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        private async Task UpdateUserStreakAsync(int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null)
                return;

            var entries = await _context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EntryDate)
                .Select(e => e.EntryDate)
                .ToListAsync();

            if (!entries.Any())
            {
                user.CurrentStreak = 0;
                user.LongestStreak = Math.Max(user.LongestStreak, 0);
                user.LastEntryDate = null;
            }
            else
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var yesterday = today.AddDays(-1);
                
                int currentStreak = 0;
                int longestStreak = 0;
                int tempStreak = 0;
                
                DateOnly? expectedDate = null;
                
                foreach (var entryDate in entries)
                {
                    if (expectedDate == null)
                    {
                        expectedDate = entryDate;
                        tempStreak = 1;
                    }
                    else if (entryDate == expectedDate.Value.AddDays(-1))
                    {
                        expectedDate = entryDate;
                        tempStreak++;
                    }
                    else if (entryDate < expectedDate.Value.AddDays(-1))
                    {
                        longestStreak = Math.Max(longestStreak, tempStreak);
                        tempStreak = 1;
                        expectedDate = entryDate;
                    }
                }
                
                longestStreak = Math.Max(longestStreak, tempStreak);
                
                // Calculate current streak based on most recent entries
                if (entries.First() == today || entries.First() == yesterday)
                {
                    currentStreak = tempStreak;
                }
                else
                {
                    currentStreak = 0;
                }
                
                user.CurrentStreak = currentStreak;
                user.LongestStreak = Math.Max(user.LongestStreak, longestStreak);
                user.LastEntryDate = entries.First();
            }
            
            await _context.SaveChangesAsync();
        }
    }
}