using System.ComponentModel.DataAnnotations;

namespace MindLog.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "User ID is required")]
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
        public string Title { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Content is required")]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
        public string Content { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Entry date is required")]
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        
        [StringLength(1000, ErrorMessage = "Tags cannot exceed 1000 characters")]
        public string? Tags { get; set; }
        
        // Navigation properties for mood tracking
        public List<JournalEntryMood> JournalEntryMoods { get; set; } = new();
        
        public User User { get; set; } = null!;
    }
}