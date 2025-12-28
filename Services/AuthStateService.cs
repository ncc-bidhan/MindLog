using Microsoft.Extensions.Logging;
using MindLog.Helpers;
using MindLog.Interfaces;
using MindLog.Models;

namespace MindLog.Services
{
    public class AuthStateService : IAuthStateService
    {
        private User? _currentUser;
        private readonly ILogger<AuthStateService> _logger;

        public AuthStateService()
        {
            _logger = Logger.GetLogger<AuthStateService>();
        }

        public User? CurrentUser
        {
            get => _currentUser;
            private set => _currentUser = value;
        }

        public bool IsAuthenticated => CurrentUser != null;

        public event Action<User?>? OnAuthStateChanged;

        public void SetUser(User? user)
        {
            CurrentUser = user;
            OnAuthStateChanged?.Invoke(user);
            
            if (user != null)
            {
                _logger.LogInformation("User authenticated: {Username}", user.Username);
            }
            else
            {
                _logger.LogInformation("User logged out");
            }
        }

        public void Logout()
        {
            _logger.LogInformation("Logout requested for user: {Username}", CurrentUser?.Username ?? "Unknown");
            CurrentUser = null;
            OnAuthStateChanged?.Invoke(null);
        }
    }
}