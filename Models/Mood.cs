using System.ComponentModel.DataAnnotations;

namespace MindLog.Models
{
    public enum MoodCategory
    {
        Positive,
        Neutral,
        Negative
    }

    public class Mood
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Icon { get; set; } = string.Empty;
        
        [Required]
        public MoodCategory Category { get; set; }
        
        [Required]
        public string Color { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
    }

    public class JournalEntryMood
    {
        public int Id { get; set; }
        
        [Required]
        public int JournalEntryId { get; set; }
        
        [Required]
        public int MoodId { get; set; }
        
        [Required]
        public bool IsPrimary { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public JournalEntry JournalEntry { get; set; } = null!;
        public Mood Mood { get; set; } = null!;
    }

    public static class MoodDefinitions
    {
        public static readonly List<Mood> PredefinedMoods = new()
        {
            // Positive Moods
            new Mood { Id = 1, Name = "Happy", Icon = "😊", Category = MoodCategory.Positive, Color = "#FFD700", Description = "Feeling joyful and content" },
            new Mood { Id = 2, Name = "Excited", Icon = "🎉", Category = MoodCategory.Positive, Color = "#FF6B6B", Description = "Enthusiastic and energized" },
            new Mood { Id = 3, Name = "Grateful", Icon = "🙏", Category = MoodCategory.Positive, Color = "#98D8C8", Description = "Appreciative and thankful" },
            new Mood { Id = 4, Name = "Peaceful", Icon = "😌", Category = MoodCategory.Positive, Color = "#87CEEB", Description = "Calm and serene" },
            new Mood { Id = 5, Name = "Confident", Icon = "💪", Category = MoodCategory.Positive, Color = "#FFA500", Description = "Self-assured and capable" },
            new Mood { Id = 6, Name = "Inspired", Icon = "✨", Category = MoodCategory.Positive, Color = "#9370DB", Description = "Creative and motivated" },
            new Mood { Id = 7, Name = "Loved", Icon = "❤️", Category = MoodCategory.Positive, Color = "#FF69B4", Description = "Feeling loved and connected" },
            
            // Neutral Moods
            new Mood { Id = 8, Name = "Neutral", Icon = "😐", Category = MoodCategory.Neutral, Color = "#808080", Description = "Neither positive nor negative" },
            new Mood { Id = 9, Name = "Tired", Icon = "😴", Category = MoodCategory.Neutral, Color = "#B0C4DE", Description = "Fatigued but not necessarily negative" },
            new Mood { Id = 10, Name = "Thoughtful", Icon = "🤔", Category = MoodCategory.Neutral, Color = "#D3D3D3", Description = "Reflective and contemplative" },
            new Mood { Id = 11, Name = "Curious", Icon = "🔍", Category = MoodCategory.Neutral, Color = "#F0E68C", Description = "Inquisitive and interested" },
            
            // Negative Moods
            new Mood { Id = 12, Name = "Sad", Icon = "😢", Category = MoodCategory.Negative, Color = "#4169E1", Description = "Feeling down or melancholic" },
            new Mood { Id = 13, Name = "Anxious", Icon = "😰", Category = MoodCategory.Negative, Color = "#FF8C00", Description = "Worried or nervous" },
            new Mood { Id = 14, Name = "Frustrated", Icon = "😤", Category = MoodCategory.Negative, Color = "#DC143C", Description = "Irritated or blocked" },
            new Mood { Id = 15, Name = "Angry", Icon = "😠", Category = MoodCategory.Negative, Color = "#B22222", Description = "Feeling anger or irritation" },
            new Mood { Id = 16, Name = "Stressed", Icon = "😣", Category = MoodCategory.Negative, Color = "#8B4513", Description = "Overwhelmed or pressured" },
            new Mood { Id = 17, Name = "Lonely", Icon = "😔", Category = MoodCategory.Negative, Color = "#696969", Description = "Feeling isolated or alone" },
            new Mood { Id = 18, Name = "Disappointed", Icon = "😞", Category = MoodCategory.Negative, Color = "#483D8B", Description = "Let down or unhappy with results" }
        };

        public static List<Mood> GetMoodsByCategory(MoodCategory category)
        {
            return PredefinedMoods.Where(m => m.Category == category && m.IsActive).ToList();
        }

        public static Mood? GetMoodById(int id)
        {
            return PredefinedMoods.FirstOrDefault(m => m.Id == id);
        }

        public static string GetCategoryColor(MoodCategory category)
        {
            return category switch
            {
                MoodCategory.Positive => "#22C55E",
                MoodCategory.Neutral => "#6B7280",
                MoodCategory.Negative => "#EF4444",
                _ => "#6B7280"
            };
        }
    }
}