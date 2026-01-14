using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindLog.Data;
using MindLog.Exceptions;
using MindLog.Helpers;
using MindLog.Interfaces;
using MindLog.Models;

namespace MindLog.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = Logger.GetLogger<AuthService>();
        }

        public async Task<User> RegisterAsync(string username, string email, string password)
        {
            try
            {
                ValidationHelper.ValidateUsername(username);
                ValidationHelper.ValidateEmail(email);
                ValidationHelper.ValidatePassword(password, Constants.Validation.MinPasswordLength);

                await using var context = _contextFactory.CreateDbContext();

                if (await context.Users.AnyAsync(u => u.Username == username))
                {
                    _logger.LogWarning("Registration attempt with existing username: {Username}", username);
                    throw new ValidationException("Username already exists.");
                }

                if (await context.Users.AnyAsync(u => u.Email == email))
                {
                    _logger.LogWarning("Registration attempt with existing email: {Email}", email);
                    throw new ValidationException("Email already exists.");
                }

                var user = new User
                {
                    Username = username,
                    Email = email,
                    Password = password
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                _logger.LogInformation("User registered successfully: {Username}", username);
                return user;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration: {Username}", username);
                throw;
            }
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            try
            {
                ValidationHelper.ValidateRequired(username, "Username");
                ValidationHelper.ValidateRequired(password, "Password");

                await using var context = _contextFactory.CreateDbContext();
                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                if (user == null)
                {
                    _logger.LogWarning("Failed login attempt for username: {Username}", username);
                    throw new AuthenticationException("Invalid username or password.");
                }

                _logger.LogInformation("User logged in successfully: {Username}", username);
                return user;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (AuthenticationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login: {Username}", username);
                throw new AuthenticationException("An error occurred during login. Please try again.");
            }
        }
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            try
            {
                ValidationHelper.ValidateRequired(currentPassword, "Current Password");
                ValidationHelper.ValidatePassword(newPassword, Constants.Validation.MinPasswordLength);

                await using var context = _contextFactory.CreateDbContext();
                var user = await context.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("Change password attempt for non-existent user ID: {UserId}", userId);
                    return false;
                }

                if (user.Password != currentPassword)
                {
                    _logger.LogWarning("Invalid current password for user ID: {UserId}", userId);
                    throw new AuthenticationException("The current password is incorrect.");
                }

                user.Password = newPassword;
                await context.SaveChangesAsync();

                _logger.LogInformation("Password changed successfully for user ID: {UserId}", userId);
                return true;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (AuthenticationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user ID: {UserId}", userId);
                throw new Exception("An error occurred while changing the password.");
            }
        }
    }
}
