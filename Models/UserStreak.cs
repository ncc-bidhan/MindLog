using System.ComponentModel.DataAnnotations;

namespace MindLog.Models
{
    public class UserStreak
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int CurrentStreak { get; set; } = 0;
        
        [Required]
        public int LongestStreak { get; set; } = 0;
        
        [Required]
        public int TotalEntries { get; set; } = 0;
        
        [Required]
        public int MissedDays { get; set; } = 0;
        
        public DateTime? LastEntryDate { get; set; }
        
        public DateTime StreakStartDate { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        public User User { get; set; } = null!;
    }
}