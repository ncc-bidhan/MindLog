namespace MindLog.Interfaces
{
    public interface IThemeService
    {
        bool IsDarkMode { get; }
        event Action<bool>? OnThemeChanged;
        Task InitializeAsync();
        Task ToggleThemeAsync();
        Task SetThemeAsync(bool isDark);
    }
}
