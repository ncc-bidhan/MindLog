using Microsoft.EntityFrameworkCore;
using MindLog.Data;
using MindLog.Models;

namespace MindLog.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> RegisterAsync(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("All fields are required");
            }

            if (password.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters long");
            }

            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                throw new ArgumentException("Username already exists");
            }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                throw new ArgumentException("Email already exists");
            }

            var user = new User
            {
                Username = username,
                Email = email,
                Password = password
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Username and password are required");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

            if (user == null)
            {
                throw new ArgumentException("Invalid username or password");
            }

            return user!;
        }
    }
}