using System.Windows;
using System.Threading;
using BlazorBase.ERPFrontServices.OrderTrackServices;
using BlazorBase.ERPFrontServices.ComplaintServices;
using BlazorBase.ERPFrontServices.ReportingServices;
using BlazorBase.ERPFrontServices.CompanyServices;
using BlazorBase.ERPFrontServices.PaymentMethodServices;
using BlazorBase.ERPFrontServices.SettingsServices;
using BackOffice.Desktop.Services;
using BlazorBase.ERPFrontServices.InventoryServices;
using POS.Core.Services.Contract.PosFeatureServices;
using POS.Core.Entities.Identity;
using Radzen;
using POS.Core.Services.Contract;
using BlazorBase.ERPFrontServices.StaffMealServices;
using BlazorBase.ERPFrontServices.LicensingServices;

namespace BackOffice.Desktop;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private ServiceProvider? _serviceProvider;
    public static List<string> InMemoryLogs { get; } = new();
    private static readonly object _logLock = new();

    public static void AddLog(string message)
    {
        lock (_logLock)
        {
            InMemoryLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (InMemoryLogs.Count > 500) InMemoryLogs.RemoveAt(0);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Register global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            base.OnStartup(e);

            // Setup Serilog for file logging
            SetupSerilog();

            Log.Information("Setting up services...");
            AddLog("Application Starting Store POS Desktop");

            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Log.Information("Creating main window...");

            // Create and show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Log.Information("Main window shown successfully");
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "POS-Logs");
            Directory.CreateDirectory(logPath);
            
            var errorFile = Path.Combine(logPath, $"startup-error-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(errorFile, $"Startup Error:\n{ex.ToString()}");
            
            Log.Fatal(ex, "Application startup failed");
            MessageBox.Show($"Failed to start application:\n{ex.Message}\n\nCheck logs at: {logPath}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void SetupSerilog()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "POS-Logs");
        Directory.CreateDirectory(logPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Error)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console()
            .WriteTo.Sink(new DelegatingSink(AddLog)) // Custom sink to push logs to our list
            .WriteTo.File(
                Path.Combine(logPath, "POS-Desktop-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Application starting up - Logs saved to: {LogPath}", logPath);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI Exception");
        ShowErrorDialog("UI Thread Error", e.Exception);
        e.Handled = true; // Prevent app from closing immediately
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "Unhandled Domain Exception");
        if (ex != null) ShowErrorDialog("Domain Error", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unobserved Task Exception");
        ShowErrorDialog("Background Task Error", e.Exception);
        e.SetObserved();
    }

    private void ShowErrorDialog(string title, Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            var message = $"Critical Error: {ex.Message}\n\nType: {ex.GetType().Name}\n\nStack Trace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                message += $"\n\nInner Exception: {ex.InnerException.Message}";
            }
            
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    // Helper sink for Serilog
    private class DelegatingSink : Serilog.Core.ILogEventSink
    {
        private readonly Action<string> _logAction;
        public DelegatingSink(Action<string> logAction) => _logAction = logAction;
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            if (logEvent.Exception != null) message += $" | EX: {logEvent.Exception.Message}";
            _logAction(message);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        // Add WPF services
        services.AddSingleton<MainWindow>();

        // Add Blazor services
        services.AddWpfBlazorWebView();
        services.AddBlazorWebViewDeveloperTools();
        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = MudBlazor.Variant.Filled;
        });
        services.AddBlazorBootstrap();
        services.AddRadzenComponents();
        services.AddBlazoredLocalStorage();
        services.AddScoped<BackOffice.Desktop.Services.IDesktopPrintingService, BackOffice.Desktop.Services.DesktopPrintingService>();

        // Localization
        services.AddLocalization();

        services.AddScoped<BlazorBase.Services.LocalizationService, DesktopLocalizationService>();

        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Configure supported cultures
        var storage = new DesktopFileStorageService();
        var cultureCode = storage.GetItemAsync<string>("culture").GetAwaiter().GetResult() ?? "ar";
        var culture = new System.Globalization.CultureInfo(cultureCode);
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

        // API Settings
        services.Configure<BlazorBase.API.ApiSettings>(configuration.GetSection("ApiSettings"));
        services.AddSingleton<BlazorBase.API.ApiSettings>(sp =>
            sp.GetRequiredService<IOptions<BlazorBase.API.ApiSettings>>().Value);

        // HTTP Client
        services.AddHttpClient(configuration["ApiSettings:ApiName"]!,
            client => { client.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"]!); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true
            });

        // Register default HttpClient as well for services that don't specify a name
        services.AddScoped(sp => 
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient(configuration["ApiSettings:ApiName"]!);
        });

        // Application services
        services.AddSingleton<CommonProperties>(sp => new CommonProperties { Language = cultureCode });
        services.AddSingleton<HandelDeliveryInvocation>();
        services.AddSingleton<CartService>();
        services.AddSingleton<Section4ButtonsServices>();
        services.AddScoped<CustomAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());
        services.AddScoped<IDineInService, DineInService>();
        services.AddScoped<IAppDateService, AppDateService>();
        services.AddScoped<IOrderSettingsService, OrderSettingsService>();
        services.AddScoped<IDeliveryServices, DeliveryServices>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<BlazorBase.ERPFrontServices.AccountServices.IAccountService, BlazorBase.ERPFrontServices.AccountServices.AccountService>();
        services.AddScoped<IPrintOrderService, DesktopPrintOrderService>();
        services.AddSingleton<CallCenterNotificationService>();

        // Category services
        services.AddScoped<ICategoryServices, CategoryService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IAttributeService, AttributeFrontService>();
        services.AddScoped<IDineInOrderFrontService, DineInOrderFrontService>();
        services.AddScoped<IOrderTrackFrontService, OrderTrackFrontService>();
        services.AddScoped<IComplaintServices, BlazorBase.ERPFrontServices.ComplaintServices.ComplaintServices>();
        services.AddScoped<IStaffMealService, StaffMealFrontService>();
        services.AddScoped<BlazorBase.ERPFrontServices.DistributionServices.IDistributionErpService, BlazorBase.ERPFrontServices.DistributionServices.DistributionErpService>();
        services.AddScoped<IVoidErpService, VoidErpService>();
        services.AddScoped<IReportingErpService, ReportingErpService>();
        services.AddScoped<BlazorBase.ERPFrontServices.ExpenseServices.IExpenseFrontService, BlazorBase.ERPFrontServices.ExpenseServices.ExpenseFrontService>();
        services.AddScoped<IPaymentMethodServices, BlazorBase.ERPFrontServices.PaymentMethodServices.PaymentMethodServices>();
        services.AddScoped<ISystemSettingsServices, SystemSettingsServices>();
        services.AddScoped<BlazorBase.ERPFrontServices.DataSyncServices.IDataSyncFrontService, BlazorBase.ERPFrontServices.DataSyncServices.DataSyncFrontService>();
        services.AddScoped<IInventoryFrontService, InventoryFrontService>();
        services.AddScoped<IRecipeFrontService, RecipeFrontService>();
        services.AddScoped<IUnitFrontService, UnitFrontService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<BlazorBase.ERPFrontServices.KdsServices.IKdsErpService, BlazorBase.ERPFrontServices.KdsServices.KdsErpService>();


        // Call Center Hub Settings
        services.Configure<CallCenterHubSettings>(configuration.GetSection("CallCenterHubs"));
        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<CallCenterHubSettings>>().Value);
        
        // Dispatcher Settings
        services.Configure<DispatcherSettings>(configuration.GetSection("DispatcherSettings"));
        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<DispatcherSettings>>().Value);
        
        // Desktop-specific services (file-based storage instead of localStorage)
        services.AddSingleton<DesktopFileStorageService>();
        services.AddScoped<DesktopFileStorageWrapper>(sp =>
        {
            var fileStorage = sp.GetRequiredService<DesktopFileStorageService>();
            return new DesktopFileStorageWrapper(fileStorage);
        });
        services.AddScoped<ICustomizationSettingsService, DesktopCustomizationSettingsService>();
        services.AddScoped<DesktopFontSizeService>();

        // Local Storage
        services.AddBlazoredLocalStorage(config =>
        {
            config.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            config.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            config.JsonSerializerOptions.IgnoreReadOnlyProperties = true;
            config.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            config.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            config.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
            config.JsonSerializerOptions.WriteIndented = false;
        });

        services.AddScoped<IPosFeatureSettingsService, DesktopPosFeatureSettingsService>();
        services.AddScoped<IPrinterServices, DesktopPrinterService>();

        // Authentication & Authorization
        services.AddAuthenticationCore();
        services.AddAuthorization(options =>
        {
            foreach (var policy in Permissions.RolePermissions)
            {
                options.AddPolicy(policy.Key, policyBuilder =>
                    policyBuilder.RequireAssertion(context =>
                    {
                        var hasPermission = context.User.HasClaim(c => 
                            c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                            c.Value == policy.Value);
                        
                        var isDenied = context.User.HasClaim(c => 
                            c.Type.Equals("deny", StringComparison.OrdinalIgnoreCase) && 
                            c.Value == policy.Value);

                        return hasPermission && !isDenied;
                    }));
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down");
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
