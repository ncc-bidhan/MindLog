namespace MindLog.Interfaces
{
    public interface IDatabaseService
    {
        Task InitializeDatabaseAsync();
        Task ResetDatabaseAsync();
    }
}
