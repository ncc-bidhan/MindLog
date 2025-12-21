using Microsoft.Extensions.Logging;
using MindLog.Services;

namespace MindLog;

public partial class App : Application
{
    public static IServiceProvider Services { get; internal set; } = null!;

    public App()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }
}