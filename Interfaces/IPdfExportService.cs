namespace MindLog.Interfaces
{
    public interface IPdfExportService
    {
        Task<byte[]> ExportJournalsToPdfAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null);
        Task ExportAndSaveJournalsAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null);
    }
}
