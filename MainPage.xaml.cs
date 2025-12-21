using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using MindLog.Services;

namespace MindLog;

public partial class MainPage : ContentPage
{
    private IServiceProvider? _services;
    
    public MainPage()
    {
        InitializeComponent();
        
        // Get services from the app
        _services = App.Services;
    }
}