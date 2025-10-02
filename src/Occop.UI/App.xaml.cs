using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Occop.Core.Authentication;
using Occop.Core.Security;
using Occop.Services.Authentication;
using Occop.Services;
using Occop.UI.ViewModels;
using Occop.UI.Views;
using Occop.UI.Services;
using System.IO;
using System.Windows;

namespace Occop.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// App.xaml的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Build configuration
                var configuration = BuildConfiguration();

                // Build host with dependency injection
                _host = CreateHost(configuration);

                // Start the host
                _host.Start();

                // Check if authentication is required on startup
                var authManager = _host.Services.GetRequiredService<AuthenticationManager>();

                if (!authManager.IsAuthenticated)
                {
                    // Show login window first
                    var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
                    var loginResult = loginWindow.ShowLoginDialog();

                    if (!loginResult)
                    {
                        // User cancelled login, exit application
                        Shutdown(0);
                        return;
                    }
                }

                // Create and show main window
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                // Log startup error and show message to user
                MessageBox.Show(
                    $"应用程序启动时发生错误：{ex.Message}",
                    "启动错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _host?.Dispose();
            }
            catch (Exception ex)
            {
                // Log shutdown error but don't prevent shutdown
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }

            base.OnExit(e);
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static IHost CreateHost(IConfiguration configuration)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(config =>
                {
                    config.AddConfiguration(configuration);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    services.AddSingleton(configuration);

                    // Logging
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddNLog();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });

                    // HTTP Client
                    services.AddHttpClient();

                    // Core Services
                    services.AddSingleton<TokenStorage>();
                    services.AddSingleton<UserWhitelist>();
                    services.AddSingleton<SecureTokenManager>();
                    services.AddSingleton<AuthenticationManager>();

                    // OAuth Services
                    services.AddSingleton<OAuthDeviceFlow>();
                    services.AddSingleton<GitHubAuthService>();

                    // UI Services
                    services.AddSingleton<ITrayManager, TrayManager>();
                    services.AddSingleton<INotificationManager, NotificationManager>();
                    services.AddSingleton<ISettingsManager, SettingsManager>();

                    // ViewModels
                    services.AddTransient<AuthenticationViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<StatusViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Views and Windows
                    services.AddTransient<AuthenticationView>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddSingleton<MainWindow>();
                })
                .UseConsoleLifetime()
                .Build();
        }
    }
}