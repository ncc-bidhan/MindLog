using System.ComponentModel.DataAnnotations;

namespace MindLog.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Streak tracking properties
        public int CurrentStreak { get; set; } = 0;
        
        public int LongestStreak { get; set; } = 0;
        
        public DateOnly? LastEntryDate { get; set; }
    }
}