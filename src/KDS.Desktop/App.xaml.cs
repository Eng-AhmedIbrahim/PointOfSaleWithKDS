using System.IO;
using System.Net.Http;
using System.Windows;
using BlazorBase;
using BlazorBase.ERPFrontServices.BranchServices;
using BlazorBase.ERPFrontServices.KdsServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Serilog;

namespace KDS.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "kds-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = new MainWindow(_serviceProvider);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start KDS Desktop application");
            MessageBox.Show($"خطأ في تشغيل شاشة المطبخ:\n{ex.Message}", "خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        IConfiguration configuration = builder.Build();
        services.AddSingleton(configuration);

        services.AddWpfBlazorWebView();
        services.AddMudServices();

        // Localization
        services.AddLocalization();
        services.AddScoped<BlazorBase.Services.LocalizationService>();

        // API Settings
        services.Configure<BlazorBase.API.ApiSettings>(configuration.GetSection("ApiSettings"));
        services.AddSingleton<BlazorBase.API.ApiSettings>(sp =>
            sp.GetRequiredService<IOptions<BlazorBase.API.ApiSettings>>().Value);

        // HTTP Client
        string apiName = configuration["ApiSettings:ApiName"] ?? "POSAPI";
        string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5050";

        services.AddHttpClient(apiName, client => { client.BaseAddress = new Uri(baseUrl); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true
            });

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient(apiName);
        });

        // Application Services
        services.AddSingleton(new CommonProperties { Language = "ar" });
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IKdsErpService, KdsErpService>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
