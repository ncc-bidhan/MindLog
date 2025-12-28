using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MindLog.Helpers;
using MindLog.Interfaces;

namespace MindLog.Services
{
    public class ThemeService : IThemeService
    {
        private bool _isDarkMode;
        private readonly ILogger<ThemeService> _logger;

        public event Action<bool>? OnThemeChanged;

        public ThemeService()
        {
            _logger = Logger.GetLogger<ThemeService>();
            _isDarkMode = LoadSavedTheme();
        }

        public bool IsDarkMode => _isDarkMode;

        private bool LoadSavedTheme()
        {
            try
            {
                var savedTheme = Preferences.Get(Constants.Theme.ThemePreferenceKey, Constants.Theme.LightTheme);
                var isDark = savedTheme.ToLowerInvariant() == Constants.Theme.DarkTheme;
                _logger.LogInformation("Loaded saved theme: {Theme}", isDark ? "Dark" : "Light");
                return isDark;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved theme, using default light theme");
                return false;
            }
        }

        private void SaveTheme()
        {
            try
            {
                Preferences.Set(Constants.Theme.ThemePreferenceKey, _isDarkMode ? Constants.Theme.DarkTheme : Constants.Theme.LightTheme);
                _logger.LogInformation("Saved theme: {Theme}", _isDarkMode ? "Dark" : "Light");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving theme preference");
            }
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("ThemeService initializing");
            _isDarkMode = LoadSavedTheme();
            OnThemeChanged?.Invoke(_isDarkMode);
            _logger.LogInformation("ThemeService initialized with theme: {Theme}", _isDarkMode ? "Dark" : "Light");
        }

        public async Task ToggleThemeAsync()
        {
            _isDarkMode = !_isDarkMode;
            SaveTheme();
            _logger.LogInformation("Theme toggled to: {Theme}", _isDarkMode ? "Dark" : "Light");
            OnThemeChanged?.Invoke(_isDarkMode);
        }

        public async Task SetThemeAsync(bool isDark)
        {
            if (_isDarkMode == isDark)
            {
                return;
            }

            _isDarkMode = isDark;
            SaveTheme();
            _logger.LogInformation("Theme set to: {Theme}", isDark ? "Dark" : "Light");
            OnThemeChanged?.Invoke(_isDarkMode);
        }
    }
}