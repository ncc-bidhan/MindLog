using Microsoft.Extensions.Logging;
using MindLog.Helpers;
using MindLog.Interfaces;

namespace MindLog.Services
{
    public class ToastService : IToastService
    {
        private readonly ILogger<ToastService> _logger;

        public event Action<bool, string, string, string>? OnShow;

        public ToastService()
        {
            _logger = Logger.GetLogger<ToastService>();
        }

        public void ShowSuccess(string title, string message, int autoCloseAfter = Constants.Toast.DefaultAutoCloseDelay)
        {
            _logger.LogInformation("Showing success toast: {Title}", title);
            OnShow?.Invoke(true, "success", title, message);
        }

        public void ShowError(string title, string message, int autoCloseAfter = 0)
        {
            _logger.LogError("Showing error toast: {Title} - {Message}", title, message);
            OnShow?.Invoke(true, "error", title, message);
        }

        public void ShowWarning(string title, string message, int autoCloseAfter = Constants.Toast.WarningAutoCloseDelay)
        {
            _logger.LogWarning("Showing warning toast: {Title}", title);
            OnShow?.Invoke(true, "warning", title, message);
        }

        public void ShowInfo(string title, string message, int autoCloseAfter = Constants.Toast.DefaultAutoCloseDelay)
        {
            _logger.LogInformation("Showing info toast: {Title}", title);
            OnShow?.Invoke(true, "info", title, message);
        }

        public void Hide()
        {
            _logger.LogDebug("Hiding toast");
            OnShow?.Invoke(false, "", "", "");
        }
    }

    public static class ToastMessages
    {
        public const string EntryCreated = "Journal entry created successfully!";
        public const string EntryUpdated = "Journal entry updated successfully!";
        public const string EntryDeleted = "Journal entry deleted successfully!";
        public const string EntryNotFound = "Journal entry not found.";
        public const string EntryExistsForDate = "An entry already exists for this date. Only one entry is allowed per day.";
        public const string ValidationError = "Please correct the errors and try again.";
        public const string NetworkError = "A network error occurred. Please check your connection and try again.";
        public const string Unauthorized = "You are not authorized to perform this action.";
    }
}