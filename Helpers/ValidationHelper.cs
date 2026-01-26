using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MindLog.Helpers
{
    public static class ValidationHelper
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UsernameRegex = new(
            @"^[a-zA-Z0-9_]{3,50}$",
            RegexOptions.Compiled);

        public static void ValidateRequired(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException($"{fieldName} is required.");
            }
        }

        public static void ValidateEmail(string email)
        {
            ValidateRequired(email, "Email");
            if (!EmailRegex.IsMatch(email))
            {
                throw new ValidationException("Invalid email format.");
            }
        }

        public static void ValidateUsername(string username)
        {
            ValidateRequired(username, "Username");
            if (!UsernameRegex.IsMatch(username))
            {
                throw new ValidationException("Username must be 3-50 characters and contain only letters, numbers, and underscores.");
            }
        }

        public static void ValidatePassword(string password, int minLength = 6)
        {
            ValidateRequired(password, "Password");
            if (password.Length < minLength)
            {
                throw new ValidationException($"Password must be at least {minLength} characters long.");
            }
        }

        public static void ValidatePin(string pin)
        {
            ValidateRequired(pin, "PIN");
            if (!Regex.IsMatch(pin, @"^\d{4,6}$"))
            {
                throw new ValidationException("PIN must be 4-6 digits long and contain only numbers.");
            }
        }

        public static void ValidateStringLength(string value, string fieldName, int minLength, int maxLength)
        {
            ValidateRequired(value, fieldName);
            if (value.Length < minLength || value.Length > maxLength)
            {
                throw new ValidationException($"{fieldName} must be between {minLength} and {maxLength} characters.");
            }
        }

        public static void ValidateDateNotInFuture(DateOnly date, string fieldName = "Date")
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (date > today)
            {
                throw new ValidationException($"{fieldName} cannot be in the future.");
            }
        }

        public static void ValidateDateRange(DateOnly date, DateOnly minDate, DateOnly maxDate, string fieldName = "Date")
        {
            if (date < minDate || date > maxDate)
            {
                throw new ValidationException($"{fieldName} must be between {minDate:MMMM d, yyyy} and {maxDate:MMMM d, yyyy}.");
            }
        }

        public static void ValidateMoodCount(List<int>? moodIds, int minCount = 1, int maxCount = 3)
        {
            if (moodIds == null || moodIds.Count < minCount)
            {
                throw new ValidationException($"At least {minCount} mood must be selected.");
            }

            if (moodIds.Count > maxCount)
            {
                throw new ValidationException($"Maximum of {maxCount} moods allowed (1 primary + {maxCount - 1} secondary).");
            }
        }

        public static void ValidateTagLength(string? tags, int maxLength = 1000)
        {
            if (tags != null && tags.Length > maxLength)
            {
                throw new ValidationException($"Tags cannot exceed {maxLength} characters.");
            }
        }

        public static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);
        }

        public static bool IsValidUsername(string username)
        {
            return !string.IsNullOrWhiteSpace(username) && UsernameRegex.IsMatch(username);
        }
    }
}
