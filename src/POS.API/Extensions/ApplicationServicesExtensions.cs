using POS.Core.Services.Contract.DineInOrderServices;
using POS.Core.Services.Contract.OrderTrackServices;
using POS.Services.DineInOrderServices;
using POS.Services.OrderTrackServices;
using POS.Core.Services.Contract.PosFeatureServices;
using POS.Services.PosFeatureServices;
using POS.Core.Services.Contract.PrintingSettings;
using POS.Services.PrintingSettings;
using POS.Services.OrderApiServices;
using POS.Services.DistributionServices;
using POS.Core.Services.Contract.VoidServices;
using POS.Services.VoidServices;
using POS.Core.Services.Contract.ComplaintServices;
using POS.Services.ComplaintServices;
using POS.Core.Services.Contract.ReportingServices;
using POS.Services.ReportingServices;
using POS.Core.Services.Contract.PaymentServices;
using POS.Services.PaymentServices;
using POS.Core.Services.Contract.EncryptionServices;
using POS.Services.EncryptionServices;
using POS.Core.Services.Contract.InventoryServices;
using POS.Services.InventoryServices;
using POS.Reports;

namespace POS.API.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));
        services.AddScoped(typeof(ICompanyService), typeof(CompanyService));
        services.AddScoped(typeof(IBranchService), typeof(BranchService));
        services.AddScoped(typeof(ICategoryService), typeof(CategoryService));
        services.AddScoped(typeof(IMenuSalesItemService), typeof(MenuSalesItemService));
        services.AddScoped(typeof(IAttributeService), typeof(AttributeService));
        services.AddScoped(typeof(IUserSetting), typeof(UserSetting));
        services.AddScoped(typeof(IAuthService), typeof(AuthService));
        services.AddScoped(typeof(IOrderService), typeof(OrderService));
        services.AddScoped(typeof(IDineInService), typeof(DineInService));
        services.AddScoped(typeof(IAppDateService), typeof(AppDateService));
        services.AddScoped(typeof(IDeliveryCustomerTitleService), typeof(DeliveryCustomerTitleService));
        services.AddScoped(typeof(IDeliveryZoneServices), typeof(DeliveryZoneServices));
        services.AddScoped(typeof(IDeliveryCustomerService), typeof(DeliveryCustomerService));
        services.AddScoped(typeof(IPrinterServices), typeof(PrinterService));
        services.AddScoped(typeof(IKitchenServices), typeof(KitchenServices));
        services.AddScoped(typeof(IKitchenPrintersService), typeof(KitchenPrinterService));
        services.AddScoped<POS.Core.Services.Contract.KitchenServices.IKdsService, POS.Services.KitchenServices.KdsService>();
        services.AddScoped(typeof(IDineInOrderService), typeof(DineInOrderService));
        services.AddScoped(typeof(IOrderTrackService), typeof(OrderTrackService));
        services.AddScoped(typeof(IPosFeatureSettingsService), typeof(PosFeatureSettingsService));
        services.AddScoped(typeof(IPrintingSettingsServices), typeof(PrintingSettingsService));
        services.AddScoped(typeof(IOrderApiServices), typeof(OrderApiServices));
        services.AddScoped(typeof(IDeliveryOrderApiServices), typeof(DeliveryOrderApiServices));
        services.AddScoped(typeof(IDistributionServices), typeof(DistributionServices));
        services.AddScoped(typeof(IComplaintService), typeof(ComplaintService));
        services.AddScoped<IVoidService, VoidService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<POS.Core.Services.Contract.DataSyncServices.IDataSyncService, POS.Services.DataSyncServices.DataSyncService>();
        services.AddScoped<POS.Core.Services.Contract.IStaffMealService, POS.Services.StaffMealServices.StaffMealService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddReportingInfrastructure();


        services.AddAutoMapper(_ => { }, typeof(MappingProfiles));
        services.AddHttpClient();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = (actionContext) =>
            {
                var errors = actionContext.ModelState.Where(e => e.Value?.Errors.Count > 0)
                                    .SelectMany(e => e.Value?.Errors ?? [])
                                    .Select(e => e.ErrorMessage)
                                    .ToArray();

                var apiValidationResponse = new ApiValidationErrorResponse()
                {
                    Errors = errors
                };
                return new BadRequestObjectResult(apiValidationResponse);
            };

        });

        return services;
    }

    public static IServiceCollection AddFlexibleCaching(
        this IServiceCollection services, string? redisConnectionString)
    {
        services.AddSingleton(typeof(IFlexibleCacheService<>), typeof(FlexibleCacheService<>));

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            Log.Warning("Redis connection string is null or empty. Falling back to in-memory caching.");
            services.AddDistributedMemoryCache();
            return services;
        }

        try
        {
            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
            Log.Information("Successfully connected to Redis.");
        }
        catch (RedisConnectionException)
        {
            Log.Warning("Failed to connect to Redis. Falling back to in-memory cache.");
            services.AddDistributedMemoryCache();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "cache error");
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}