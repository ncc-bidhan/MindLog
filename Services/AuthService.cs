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

        public async Task<User> RegisterAsync(string username, string email, string pin)
        {
            try
            {
                ValidationHelper.ValidateUsername(username);
                ValidationHelper.ValidateEmail(email);
                ValidationHelper.ValidatePin(pin);

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
                    Pin = pin
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

        public async Task<User> LoginWithPinAsync(string pin)
        {
            try
            {
                ValidationHelper.ValidateRequired(pin, "PIN");
                ValidationHelper.ValidatePin(pin);

                await using var context = _contextFactory.CreateDbContext();
                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Pin == pin);

                if (user == null)
                {
                    _logger.LogWarning("Failed login attempt with PIN");
                    throw new AuthenticationException("Invalid PIN.");
                }

                _logger.LogInformation("User logged in successfully with PIN");
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
                _logger.LogError(ex, "Error during user login with PIN");
                throw new AuthenticationException("An error occurred during login. Please try again.");
            }
        }
        public async Task<bool> ChangePinAsync(int userId, string currentPin, string newPin)
        {
            try
            {
                ValidationHelper.ValidateRequired(currentPin, "Current PIN");
                ValidationHelper.ValidatePin(newPin);

                await using var context = _contextFactory.CreateDbContext();
                var user = await context.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("Change password attempt for non-existent user ID: {UserId}", userId);
                    return false;
                }

                if (user.Pin != currentPin)
                {
                    _logger.LogWarning("Invalid current PIN for user ID: {UserId}", userId);
                    throw new AuthenticationException("The current PIN is incorrect.");
                }

                user.Pin = newPin;
                await context.SaveChangesAsync();

                _logger.LogInformation("PIN changed successfully for user ID: {UserId}", userId);
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
                _logger.LogError(ex, "Error changing PIN for user ID: {UserId}", userId);
                throw new Exception("An error occurred while changing the PIN.");
            }
        }

        public async Task<bool> HasAnyUsersAsync()
        {
            try
            {
                await using var context = _contextFactory.CreateDbContext();
                return await context.Users.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if any users exist");
                return false;
            }
        }
    }
}
