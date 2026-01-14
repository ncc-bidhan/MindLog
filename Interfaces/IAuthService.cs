using MindLog.Models;

namespace MindLog.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(string username, string email, string password);
        Task<User> LoginAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    }
}
