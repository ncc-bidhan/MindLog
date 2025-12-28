using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui;
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

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "mindlog.db");
            options.UseSqlite($"Data Source={databasePath}");
        });

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddSingleton<IAuthStateService, AuthStateService>();
        builder.Services.AddSingleton<AuthStateService>();
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
        builder.Services.AddScoped<JournalEntryService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<ToastService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddScoped<IStreakService, StreakService>();
        builder.Services.AddScoped<StreakService>();
        builder.Services.AddSingleton<IPdfExportService, PdfExportService>();
        builder.Services.AddSingleton<PdfExportService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        
        App.Services = app.Services;

        var databaseService = app.Services.GetRequiredService<IDatabaseService>();
        databaseService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        return app;
    }
}