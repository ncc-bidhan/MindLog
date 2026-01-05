using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui;
using MudBlazor.Services;
using MindLog.Data;
using MindLog.Interfaces;
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
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => 
            { 
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); 
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "mindlog.db");
            options.UseSqlite($"Data Source={databasePath}");
        });

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddSingleton<IAuthStateService, AuthStateService>();
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddScoped<IStreakService, StreakService>();
        builder.Services.AddSingleton<IPdfExportService, PdfExportService>();
        builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Register implementations directly for components that inject them by class name
        builder.Services.AddScoped(sp => (AuthService)sp.GetRequiredService<IAuthService>());
        builder.Services.AddSingleton(sp => (AuthStateService)sp.GetRequiredService<IAuthStateService>());
        builder.Services.AddSingleton(sp => (DatabaseService)sp.GetRequiredService<IDatabaseService>());
        builder.Services.AddScoped(sp => (JournalEntryService)sp.GetRequiredService<IJournalEntryService>());
        builder.Services.AddSingleton(sp => (ToastService)sp.GetRequiredService<IToastService>());
        builder.Services.AddSingleton(sp => (ThemeService)sp.GetRequiredService<IThemeService>());
        builder.Services.AddScoped(sp => (StreakService)sp.GetRequiredService<IStreakService>());
        builder.Services.AddSingleton(sp => (PdfExportService)sp.GetRequiredService<IPdfExportService>());
        builder.Services.AddScoped(sp => (AnalyticsService)sp.GetRequiredService<IAnalyticsService>());

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        
        App.Services = app.Services;

        try
        {
            var databaseService = app.Services.GetRequiredService<IDatabaseService>();
            databaseService.InitializeDatabaseAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the app
            var logger = app.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(ex, "Failed to initialize database during startup");
        }

        return app;
    }
}