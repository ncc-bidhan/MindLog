using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MindLog.Data;
using MindLog.Models;
using MindLog.Services;

namespace MindLog;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "mindlog.db");
            options.UseSqlite($"Data Source={databasePath}");
        });
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<AuthStateService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<JournalEntryService>();
        builder.Services.AddSingleton<ToastService>();
        builder.Services.AddSingleton<ThemeService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        
        // Initialize the app with services
        App.Services = app.Services;

        // Initialize database
        var databaseService = app.Services.GetRequiredService<DatabaseService>();
        databaseService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        return app;
    }
}