namespace MindLog.Interfaces
{
    public interface IToastService
    {
        event Action<bool, string, string, string>? OnShow;
        void ShowSuccess(string title, string message, int autoCloseAfter = 5000);
        void ShowError(string title, string message, int autoCloseAfter = 0);
        void ShowWarning(string title, string message, int autoCloseAfter = 8000);
        void ShowInfo(string title, string message, int autoCloseAfter = 5000);
        void Hide();
    }
}
