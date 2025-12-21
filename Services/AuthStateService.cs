using MindLog.Models;

namespace MindLog.Services
{
    public class AuthStateService
    {
        public User? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;

        public event Action<User?>? OnAuthStateChanged;

        public void SetUser(User? user)
        {
            CurrentUser = user;
            OnAuthStateChanged?.Invoke(user);
        }

        public void Logout()
        {
            CurrentUser = null;
            OnAuthStateChanged?.Invoke(null);
        }
    }
}