namespace MindLog.Helpers
{
    public static class Constants
    {
        public static class Validation
        {
            public const int MinPasswordLength = 6;
            public const int MaxPasswordLength = 100;
            public const int MinUsernameLength = 3;
            public const int MaxUsernameLength = 50;
            public const int MaxEmailLength = 100;
            public const int MinTitleLength = 1;
            public const int MaxTitleLength = 200;
            public const int MinContentLength = 1;
            public const int MaxContentLength = 10000;
            public const int MaxTagsLength = 1000;
            public const int MinMoodCount = 1;
            public const int MaxMoodCount = 3;
            public const int MaxEntryYearsBack = 10;
        }

        public static class Pagination
        {
            public const int DefaultPageSize = 10;
            public const int MaxPageSize = 100;
        }

        public static class Theme
        {
            public const string LightTheme = "light";
            public const string DarkTheme = "dark";
            public const string ThemePreferenceKey = "theme";
        }

        public static class Toast
        {
            public const int DefaultAutoCloseDelay = 5000;
            public const int WarningAutoCloseDelay = 8000;
        }

        public static class Database
        {
            public const string DatabaseFileName = "mindlog.db";
            public const string RequiredTables = "Users,JournalEntries,Moods,JournalEntryMoods,UserStreaks";
        }

        public static class PDF
        {
            public const string FontKey = "MindLogFont";
            public const string DefaultFont = "Arial";
        }
    }
}
