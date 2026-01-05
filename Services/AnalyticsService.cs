using Microsoft.EntityFrameworkCore;
using MindLog.Data;
using MindLog.Models;

namespace MindLog.Services
{
    public interface IAnalyticsService
    {
        Task<AnalyticsData> GetAnalyticsDataAsync(int userId);
        Task<List<MoodDistribution>> GetMoodDistributionAsync(int userId);
        Task<List<MoodFrequency>> GetMostFrequentMoodsAsync(int userId, int topN = 5);
        Task<List<TagFrequency>> GetMostUsedTagsAsync(int userId, int topN = 10);
        Task<List<TagBreakdown>> GetTagBreakdownAsync(int userId);
        Task<List<WordCountTrend>> GetWordCountTrendsAsync(int userId, int days = 30);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AnalyticsService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<AnalyticsData> GetAnalyticsDataAsync(int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var entries = await context.JournalEntries
                    .Where(e => e.UserId == userId)
                    .Select(e => new { e.Content, e.Tags })
                    .ToListAsync();

                var totalEntries = entries.Count;
                var totalWords = entries.Sum(e => e.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                
                var entriesWithTags = entries.Count(e => !string.IsNullOrWhiteSpace(e.Tags));
                
                var uniqueTags = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Tags))
                    .SelectMany(e => e.Tags.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim().ToLower())
                    .Distinct()
                    .Count();

                var averageEntryLength = totalEntries > 0
                    ? (double)entries.Average(e => e.Content.Length)
                    : 0;

                return new AnalyticsData
                {
                    TotalEntries = totalEntries,
                    TotalWords = totalWords,
                    EntriesWithTags = entriesWithTags,
                    UniqueTagsCount = uniqueTags,
                    AverageEntryLength = (int)averageEntryLength,
                    MoodDistribution = await GetMoodDistributionAsync(userId),
                    MostFrequentMoods = await GetMostFrequentMoodsAsync(userId),
                    MostUsedTags = await GetMostUsedTagsAsync(userId),
                    WordCountTrends = await GetWordCountTrendsAsync(userId)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAnalyticsDataAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MoodDistribution>> GetMoodDistributionAsync(int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var moodCounts = await context.JournalEntryMoods
                    .Where(jem => jem.JournalEntry.UserId == userId)
                    .GroupBy(jem => jem.Mood)
                    .Select(g => new MoodDistribution
                    {
                        MoodId = g.Key.Id,
                        MoodName = g.Key.Name,
                        MoodIcon = g.Key.Icon,
                        MoodColor = g.Key.Color,
                        MoodCategory = g.Key.Category,
                        Count = g.Count()
                    })
                    .OrderByDescending(m => m.Count)
                    .ToListAsync();

                return moodCounts;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMoodDistributionAsync: {ex.Message}");
                return new List<MoodDistribution>();
            }
        }

        public async Task<List<MoodFrequency>> GetMostFrequentMoodsAsync(int userId, int topN = 5)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var moodFrequencies = await context.JournalEntryMoods
                    .Where(jem => jem.JournalEntry.UserId == userId)
                    .GroupBy(jem => jem.Mood)
                    .Select(g => new MoodFrequency
                    {
                        MoodId = g.Key.Id,
                        MoodName = g.Key.Name,
                        MoodIcon = g.Key.Icon,
                        MoodColor = g.Key.Color,
                        Count = g.Count(),
                        Percentage = (double)g.Count() / context.JournalEntryMoods
                            .Count(jem => jem.JournalEntry.UserId == userId) * 100
                    })
                    .OrderByDescending(m => m.Count)
                    .Take(topN)
                    .ToListAsync();

                return moodFrequencies;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMostFrequentMoodsAsync: {ex.Message}");
                return new List<MoodFrequency>();
            }
        }

        public async Task<List<TagFrequency>> GetMostUsedTagsAsync(int userId, int topN = 10)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var entries = await context.JournalEntries
                    .Where(e => e.UserId == userId && e.Tags != null && e.Tags.Length > 0)
                    .Select(e => e.Tags)
                    .ToListAsync();

                var tags = entries
                    .SelectMany(t => t.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .GroupBy(t => t)
                    .Select(g => new TagFrequency
                    {
                        Tag = g.Key,
                        Count = g.Count(),
                        Percentage = entries.Count > 0 ? (double)g.Count() / entries.Count * 100 : 0
                    })
                    .OrderByDescending(t => t.Count)
                    .Take(topN)
                    .ToList();

                return tags;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMostUsedTagsAsync: {ex.Message}");
                return new List<TagFrequency>();
            }
        }

        public async Task<List<TagBreakdown>> GetTagBreakdownAsync(int userId)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var entries = await context.JournalEntries
                    .Where(e => e.UserId == userId && e.Tags != null && e.Tags.Length > 0)
                    .Select(e => new { e.Tags, e.EntryDate, e.Id })
                    .ToListAsync();

                var allTags = entries
                    .SelectMany(e => e.Tags.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                var breakdown = new List<TagBreakdown>();
                foreach (var tag in allTags)
                {
                    var tagEntries = entries.Where(e => e.Tags.Contains(tag)).ToList();
                    var entryIds = tagEntries.Select(e => e.Id).ToList();

                    var associatedMoods = await context.JournalEntryMoods
                        .Where(jem => entryIds.Contains(jem.JournalEntryId))
                        .GroupBy(jem => jem.Mood.Name)
                        .Select(m => new MoodAssociation
                        {
                            MoodName = m.Key,
                            Count = m.Count()
                        })
                        .OrderByDescending(m => m.Count)
                        .Take(3)
                        .ToListAsync();

                    breakdown.Add(new TagBreakdown
                    {
                        Tag = tag,
                        TotalEntries = tagEntries.Count,
                        FirstUsed = tagEntries.Min(e => e.EntryDate),
                        LastUsed = tagEntries.Max(e => e.EntryDate),
                        AssociatedMoods = associatedMoods
                    });
                }

                return breakdown.OrderByDescending(t => t.TotalEntries).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTagBreakdownAsync: {ex.Message}");
                return new List<TagBreakdown>();
            }
        }

        public async Task<List<WordCountTrend>> GetWordCountTrendsAsync(int userId, int days = 30)
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();

                var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));

                var entries = await context.JournalEntries
                    .Where(e => e.UserId == userId && e.EntryDate >= startDate)
                    .Select(e => new { e.EntryDate, e.Content })
                    .ToListAsync();

                var trends = entries
                    .GroupBy(e => e.EntryDate)
                    .Select(g => new WordCountTrend
                    {
                        Date = g.Key,
                        WordCount = g.Sum(e => e.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
                        EntryCount = g.Count()
                    })
                    .OrderBy(t => t.Date)
                    .ToList();

                return trends;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetWordCountTrendsAsync: {ex.Message}");
                return new List<WordCountTrend>();
            }
        }
    }

    public class AnalyticsData
    {
        public int TotalEntries { get; set; }
        public int TotalWords { get; set; }
        public int EntriesWithTags { get; set; }
        public int UniqueTagsCount { get; set; }
        public int AverageEntryLength { get; set; }
        public List<MoodDistribution> MoodDistribution { get; set; } = new();
        public List<MoodFrequency> MostFrequentMoods { get; set; } = new();
        public List<TagFrequency> MostUsedTags { get; set; } = new();
        public List<WordCountTrend> WordCountTrends { get; set; } = new();
    }

    public class MoodDistribution
    {
        public int MoodId { get; set; }
        public string MoodName { get; set; } = string.Empty;
        public string MoodIcon { get; set; } = string.Empty;
        public string MoodColor { get; set; } = string.Empty;
        public MoodCategory MoodCategory { get; set; }
        public int Count { get; set; }
    }

    public class MoodFrequency
    {
        public int MoodId { get; set; }
        public string MoodName { get; set; } = string.Empty;
        public string MoodIcon { get; set; } = string.Empty;
        public string MoodColor { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TagFrequency
    {
        public string Tag { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TagBreakdown
    {
        public string Tag { get; set; } = string.Empty;
        public int TotalEntries { get; set; }
        public DateOnly FirstUsed { get; set; }
        public DateOnly LastUsed { get; set; }
        public List<MoodAssociation> AssociatedMoods { get; set; } = new();
    }

    public class MoodAssociation
    {
        public string MoodName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class WordCountTrend
    {
        public DateOnly Date { get; set; }
        public int WordCount { get; set; }
        public int EntryCount { get; set; }
    }
}
