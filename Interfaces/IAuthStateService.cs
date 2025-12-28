using MindLog.Models;

namespace MindLog.Interfaces
{
    public interface IAuthStateService
    {
        User? CurrentUser { get; }
        bool IsAuthenticated { get; }
        event Action<User?>? OnAuthStateChanged;
        void SetUser(User? user);
        void Logout();
    }
}
