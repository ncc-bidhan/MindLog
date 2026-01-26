using MindLog.Models;

namespace MindLog.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(string username, string email, string pin);
        Task<User> LoginWithPinAsync(string pin);
        Task<bool> ChangePinAsync(int userId, string currentPin, string newPin);
        Task<bool> HasAnyUsersAsync();
    }
}
