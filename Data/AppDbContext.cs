using Microsoft.EntityFrameworkCore;
using MindLog.Models;

namespace MindLog.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<Mood> Moods { get; set; }
        public DbSet<JournalEntryMood> JournalEntryMoods { get; set; }
        public DbSet<UserStreak> UserStreaks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.UserId, e.EntryDate }).IsUnique();
            });

            modelBuilder.Entity<Mood>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                
                entity.Property(e => e.Category)
                      .HasConversion<string>();
            });

            modelBuilder.Entity<JournalEntryMood>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.JournalEntry)
                      .WithMany(je => je.JournalEntryMoods)
                      .HasForeignKey(e => e.JournalEntryId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Mood)
                      .WithMany()
                      .HasForeignKey(e => e.MoodId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                // Ensure unique combination of entry and mood
                entity.HasIndex(e => new { e.JournalEntryId, e.MoodId }).IsUnique();
            });

            modelBuilder.Entity<UserStreak>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithOne()
                      .HasForeignKey<UserStreak>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            // Seed predefined moods
            modelBuilder.Entity<Mood>().HasData(MoodDefinitions.PredefinedMoods);
        }
    }
}