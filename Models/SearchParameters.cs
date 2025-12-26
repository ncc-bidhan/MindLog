namespace MindLog.Models
{
    public class SearchParameters
    {
        public string? SearchTerm { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public List<int>? MoodIds { get; set; }
        public List<string>? Tags { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}