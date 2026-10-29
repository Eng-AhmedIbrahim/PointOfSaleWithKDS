namespace Pos.Repository.Data;

using POS.Core.Entities.Settings;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Configure decimal properties to avoid truncation warnings
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var properties = entityType.GetProperties()
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

            foreach (var property in properties)
            {
                property.SetColumnType("decimal(18,2)");
            }
        }

        modelBuilder.Entity<License>()
            .HasKey(l => new { l.CustomerID, l.BranchID, l.LicenseKey });
    }

    public DbSet<AttributeItem> AttributeItems { get; set; }
    public DbSet<AttributeGroup> AttributeGroups { get; set; }
    public DbSet<Attributes> Attributes { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<KitchenType> KitchenTypes { get; set; }
    public DbSet<KitchenPrinters> KitchenPrinters { get; set; }
    public DbSet<MenuSalesItems> MenuSalesItems { get; set; }
    public DbSet<ItemsClassifications> ItemsClassifications { get; set; }
    public DbSet<OrderItemAttributes> OrderItemAttributes { get; set; }
    public DbSet<Orders> Orders { get; set; }
    public DbSet<OrderItemsDetails> OrdersDetails { get; set; }
    public DbSet<OrderItemComment> OrderItemComments { get; set; }
    public DbSet<OrderSetting> OrderSettings { get; set; }
    public DbSet<PrintingSettings> PrintingSettings { get; set; }
    public DbSet<TakeawayCustomer> TakeawayCustomers { get; set; }
    public DbSet<TableGroup> TableGroups { get; set; }
    public DbSet<Table> Tables { get; set; }
    public DbSet<DineInOrder> DineInOrders { get; set; }
    public DbSet<AppDate> AppDate { get; set; }
    public DbSet<ShiftHandover> ShiftHandovers { get; set; }
    public DbSet<DeliveryCompanyInfo> DeliveryCompanyInfo { get; set; }
    public DbSet<DeliveryCustomerTitle> DeliveryCustomerTitle { get; set; }
    public DbSet<DeliveryZone> DeliveryZones { get; set; }
    public DbSet<CustomerAddress> CustomerAddress { get; set; }
    public DbSet<OrderTrack> OrderTracks { get; set; }
    public DbSet<POS.Core.Entities.ReservationEntity.Reservation> Reservations { get; set; }
    public DbSet<PosFeatureSetting> PosFeatureSettings { get; set; }
    public DbSet<POS.Core.Entities.ComplaintEntity.Complaint> Complaints { get; set; }
    public DbSet<OrderVoid> OrderVoids { get; set; }
    public DbSet<OrderVoidItem> OrderVoidItems { get; set; }
    public DbSet<POS.Core.Entities.Payment.PaymentMethodEntity> PaymentMethods { get; set; }
    public DbSet<POS.Core.Entities.Payment.Expense> Expenses { get; set; }
    public DbSet<POS.Core.Entities.Settings.DispatcherSetting> DispatcherSettings { get; set; }
    public DbSet<HqSetting> HqSettings { get; set; }
    public DbSet<LicensedDevice> LicensedDevices { get; set; }
    public DbSet<License> Licenses { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }

    public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
    public DbSet<InventoryTransactionImage> InventoryTransactionImages { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<ItemType> ItemTypes { get; set; }

    // Staff Meals
    public DbSet<POS.Core.Entities.StaffMealEntity.StaffMealConfig> StaffMealConfigs { get; set; }
    public DbSet<POS.Core.Entities.StaffMealEntity.StaffMealGroup> StaffMealGroups { get; set; }
    public DbSet<POS.Core.Entities.StaffMealEntity.StaffMealGroupItem> StaffMealGroupItems { get; set; }
    public DbSet<POS.Core.Entities.StaffMealEntity.StaffMealUsage> StaffMealUsages { get; set; }

    // KDS & Pickup Screens
    public DbSet<POS.Core.Entities.Kitchen.KdsScreen> KdsScreens { get; set; }
}