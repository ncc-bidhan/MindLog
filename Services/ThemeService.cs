using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace MindLog.Services
{
    public class ThemeService
    {
        private bool _isDarkMode = false;

        public event Action<bool>? OnThemeChanged;

        public ThemeService()
        {
            // Initialize with saved theme or default to light
            LoadSavedTheme();
        }

        public bool IsDarkMode => _isDarkMode;

        private void LoadSavedTheme()
        {
            try
            {
                var savedTheme = Preferences.Get("theme", "light");
                _isDarkMode = savedTheme.ToLower() == "dark";
                Console.WriteLine($"ThemeService: Loaded saved theme: {savedTheme}, isDark: {_isDarkMode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error loading saved theme: {ex.Message}");
                _isDarkMode = false; // Default to light theme
            }
        }

        private void SaveTheme()
        {
            try
            {
                Preferences.Set("theme", _isDarkMode ? "dark" : "light");
                Console.WriteLine($"ThemeService: Saved theme: {(_isDarkMode ? "dark" : "light")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error saving theme: {ex.Message}");
            }
        }

        public async Task InitializeAsync()
        {
            Console.WriteLine("ThemeService: Initializing...");
            
            // Load the saved theme
            LoadSavedTheme();
            
            Console.WriteLine($"ThemeService: Initialization complete. Current theme: {(_isDarkMode ? "dark" : "light")}");
            OnThemeChanged?.Invoke(_isDarkMode);
        }

        public async Task ToggleThemeAsync()
        {
            _isDarkMode = !_isDarkMode;
            SaveTheme();
            Console.WriteLine($"ThemeService: Theme toggled to: {(_isDarkMode ? "dark" : "light")}");
            OnThemeChanged?.Invoke(_isDarkMode);
        }

        public async Task SetThemeAsync(bool isDark)
        {
            Console.WriteLine($"ThemeService.SetThemeAsync called with isDark: {isDark}, current theme: {_isDarkMode}");
            
            if (_isDarkMode == isDark)
            {
                Console.WriteLine("ThemeService: Theme is already the requested value, returning");
                return;
            }

            _isDarkMode = isDark;
            SaveTheme();
            
            Console.WriteLine($"ThemeService: Theme set to: {(isDark ? "dark" : "light")}");
            Console.WriteLine($"ThemeService: Invoking OnThemeChanged with: {_isDarkMode}");
            OnThemeChanged?.Invoke(_isDarkMode);
            Console.WriteLine($"ThemeService: OnThemeChanged invoked");
        }

        // Debug method to check current theme state
        public string GetCurrentThemeDebug()
        {
            var savedTheme = Preferences.Get("theme", "light");
            return $"Current theme: {_isDarkMode}, Saved theme: {savedTheme}";
        }
    }
}