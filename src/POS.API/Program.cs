using POS.API.Hubs;
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5050", "http://localhost:5095");


builder.AddSerilogService();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var encryptedConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseConnectionString = EncryptionHelper.DecryptString(encryptedConnectionString!);
builder.Services.AddControllers();
builder.Services.AddSwaggerServices();
builder.Services.AddApplicationServices();
builder.Services.AddSignalR();

var callCenterSettings = builder.Configuration.GetSection("CallCenterSettings")
    .Get<CallCenterSettings>() ?? new CallCenterSettings();
builder.Services.AddSingleton(callCenterSettings);

var isDispatcher = callCenterSettings.IsDispatcher 
    || (builder.Configuration.GetSection("DispatcherSettings")?.GetValue<bool>("IsDispatcher") ?? false);

// Only run if Central Call Center OR Dispatcher
if (callCenterSettings.IsCentralCallCenter || isDispatcher)
{
    builder.Services.AddHostedService<OrderRetryBackgroundService>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", options =>
    {
        options.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true) // Allow any origin for SignalR
            .AllowCredentials(); // Required for SignalR
    });
});


#region Database connections


builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(databaseConnectionString);
});

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
{
    options.UseSqlServer(databaseConnectionString);
});

#endregion

builder.Services.AddIdentityServices(builder.Configuration);

var app = builder.Build();

#region Database Migrate

using var scope = app.Services.CreateScope();

var services = scope.ServiceProvider;

var dbContext = services.GetRequiredService<AppDbContext>();
var identityDbContext = services.GetRequiredService<AppIdentityDbContext>();
var userManager = services.GetRequiredService<UserManager<AppUser>>();
var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

var loggerFactory = services.GetRequiredService<ILoggerFactory>();

try
{
    dbContext.Database.SetCommandTimeout(180);
    identityDbContext.Database.SetCommandTimeout(180);

    await dbContext.Database.MigrateAsync();
    await identityDbContext.Database.MigrateAsync();

    // Hotfix: Ensure Permissions table schema is correct if migrations were out of sync
    await identityDbContext.Database.ExecuteSqlRawAsync(@"
        -- 1. Rename NameEn to ScreenEnglishName if it exists and target doesn't
        IF COL_LENGTH('Permissions', 'NameEn') IS NOT NULL AND COL_LENGTH('Permissions', 'ScreenEnglishName') IS NULL
            EXEC sp_rename 'Permissions.NameEn', 'ScreenEnglishName', 'COLUMN';

        -- 2. Rename NameAr to ScreenArabicName if it exists and target doesn't
        IF COL_LENGTH('Permissions', 'NameAr') IS NOT NULL AND COL_LENGTH('Permissions', 'ScreenArabicName') IS NULL
            EXEC sp_rename 'Permissions.NameAr', 'ScreenArabicName', 'COLUMN';

        -- 3. Add PoliceArabicName if missing
        IF COL_LENGTH('Permissions', 'PoliceArabicName') IS NULL
            ALTER TABLE Permissions ADD PoliceArabicName nvarchar(max) NOT NULL DEFAULT '';

        -- 4. Add PoliceEnglishNameEn if missing
        IF COL_LENGTH('Permissions', 'PoliceEnglishNameEn') IS NULL
            ALTER TABLE Permissions ADD PoliceEnglishNameEn nvarchar(max) NOT NULL DEFAULT '';

        -- 5. Add IsBackOffice if missing
        IF COL_LENGTH('Permissions', 'IsBackOffice') IS NULL
            ALTER TABLE Permissions ADD IsBackOffice bit NOT NULL DEFAULT 0;

        -- 6. Fallback: Add ScreenArabicName if still missing after rename
        IF COL_LENGTH('Permissions', 'ScreenArabicName') IS NULL
            ALTER TABLE Permissions ADD ScreenArabicName nvarchar(max) NOT NULL DEFAULT '';

        -- 7. Fallback: Add ScreenEnglishName if still missing after rename
        IF COL_LENGTH('Permissions', 'ScreenEnglishName') IS NULL
            ALTER TABLE Permissions ADD ScreenEnglishName nvarchar(max) NOT NULL DEFAULT '';

        -- Ensure new void & edit permissions are in Permissions table
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = 'CanAccessDeliveryVoidBtn')
            INSERT INTO Permissions (Name, PoliceArabicName, PoliceEnglishNameEn, ScreenArabicName, ScreenEnglishName, IsBackOffice)
            VALUES ('CanAccessDeliveryVoidBtn', N'إلغاء طلب دليفري', 'Void Delivery Order', N'شاشة التوصيل', 'Delivery Screen', 0);
        ELSE
            UPDATE Permissions SET PoliceArabicName = N'إلغاء طلب دليفري', ScreenArabicName = N'شاشة التوصيل' WHERE Name = 'CanAccessDeliveryVoidBtn';

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = 'CanAccessDeliveryEditBtn')
            INSERT INTO Permissions (Name, PoliceArabicName, PoliceEnglishNameEn, ScreenArabicName, ScreenEnglishName, IsBackOffice)
            VALUES ('CanAccessDeliveryEditBtn', N'تعديل طلب دليفري', 'Edit Delivery Order', N'شاشة التوصيل', 'Delivery Screen', 0);
        ELSE
            UPDATE Permissions SET PoliceArabicName = N'تعديل طلب دليفري', ScreenArabicName = N'شاشة التوصيل' WHERE Name = 'CanAccessDeliveryEditBtn';

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = 'CanVoidBranchDeliveryOrder')
            INSERT INTO Permissions (Name, PoliceArabicName, PoliceEnglishNameEn, ScreenArabicName, ScreenEnglishName, IsBackOffice)
            VALUES ('CanVoidBranchDeliveryOrder', N'إلغاء طلب الفروع', 'Void Branch Order', N'أوردرات الفروع', 'Branch Orders', 0);
        ELSE
            UPDATE Permissions SET PoliceArabicName = N'إلغاء طلب الفروع', ScreenArabicName = N'أوردرات الفروع' WHERE Name = 'CanVoidBranchDeliveryOrder';
    ");

    // Hotfix: Remove IDENTITY from Items, Categories, Attributes, and AttributeItems (Development Only - Tables are empty)
    await dbContext.Database.ExecuteSqlRawAsync(@"
        -- 0. Create ItemsClassifications without Identity if not exists
        IF OBJECT_ID('ItemsClassifications') IS NULL
        BEGIN
            CREATE TABLE ItemsClassifications (
                [Id] INT NOT NULL PRIMARY KEY,
                [Name] NVARCHAR(100) NOT NULL,
                [ArabicName] NVARCHAR(100) NULL
            );
        END

        IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id IN (OBJECT_ID('Categories'), OBJECT_ID('MenuSalesItems'), OBJECT_ID('Attributes'), OBJECT_ID('AttributeItems')))
        BEGIN
            -- 1. Drop all potentially conflicting FKs
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MenuSalesItems_Categories_CategoryId')
                ALTER TABLE MenuSalesItems DROP CONSTRAINT FK_MenuSalesItems_Categories_CategoryId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MenuSalesItems_ItemsClassifications_MainCategoryId')
                ALTER TABLE MenuSalesItems DROP CONSTRAINT FK_MenuSalesItems_ItemsClassifications_MainCategoryId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AttributeItems_Attributes_AttributeId')
                ALTER TABLE AttributeItems DROP CONSTRAINT FK_AttributeItems_Attributes_AttributeId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AttributeItems_MenuSalesItems_RelatedMenuItemId')
                ALTER TABLE AttributeItems DROP CONSTRAINT FK_AttributeItems_MenuSalesItems_RelatedMenuItemId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdersDetails_MenuSalesItems_ItemId')
                ALTER TABLE OrdersDetails DROP CONSTRAINT FK_OrdersDetails_MenuSalesItems_ItemId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrdersDetails_MenuSalesItems_MenuSalesItemId')
                ALTER TABLE OrdersDetails DROP CONSTRAINT FK_OrdersDetails_MenuSalesItems_MenuSalesItemId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MenuSalesItems_Attributes_AttributeId')
                ALTER TABLE MenuSalesItems DROP CONSTRAINT FK_MenuSalesItems_Attributes_AttributeId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrderItemAttributes_MenuSalesItems_MenuSalesItemId')
                ALTER TABLE OrderItemAttributes DROP CONSTRAINT FK_OrderItemAttributes_MenuSalesItems_MenuSalesItemId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrderItemAttributes_MenuSalesItems_ItemId')
                ALTER TABLE OrderItemAttributes DROP CONSTRAINT FK_OrderItemAttributes_MenuSalesItems_ItemId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrderItemAttributes_AttributeItems_AttributeItemId')
                ALTER TABLE OrderItemAttributes DROP CONSTRAINT FK_OrderItemAttributes_AttributeItems_AttributeItemId;

            -- 1b. Fallback for existing tables without identity
            IF COL_LENGTH('MenuSalesItems', 'ByWeight') IS NULL AND OBJECT_ID('MenuSalesItems') IS NOT NULL
                ALTER TABLE MenuSalesItems ADD ByWeight BIT NOT NULL DEFAULT 0;

            IF COL_LENGTH('MenuSalesItems', 'MainCategoryId') IS NOT NULL AND OBJECT_ID('MenuSalesItems') IS NOT NULL
            BEGIN
                -- If it's nvarchar (old type), drop and recreate or alter
                DECLARE @dataType NVARCHAR(128) = (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MenuSalesItems' AND COLUMN_NAME = 'MainCategoryId');
                IF @dataType = 'nvarchar'
                BEGIN
                     -- Safest to just drop and let the identity check handle recreation, 
                     -- or if identity is already gone, manually alter.
                     -- But since we are in dev, let's just make sure it's INT.
                     ALTER TABLE MenuSalesItems ALTER COLUMN MainCategoryId INT NULL;
                END
            END

            -- 2. Drop and Recreate Categories without Identity
            IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('Categories'))
            BEGIN
                DROP TABLE Categories;
                CREATE TABLE Categories ( 
                    [Id] INT NOT NULL PRIMARY KEY, 
                    [ArabicName] NVARCHAR(70) NOT NULL, 
                    [EnglishName] NVARCHAR(70) NOT NULL, 
                    [NormalizedEnglishName] NVARCHAR(70) NOT NULL, 
                    [Invisible] BIT NOT NULL DEFAULT 0, 
                    [ItemsFont] NVARCHAR(70) NULL, 
                    [UpdateDate] DATETIME NULL, 
                    [PrintInBackupReceipt] BIT NOT NULL DEFAULT 1, 
                    [CreationDate] DATETIME NOT NULL, 
                    [BranchId] INT NOT NULL DEFAULT 1, 
                    [KitchenTypeId] INT NULL 
                );
            END

            -- 3. Drop and Recreate Attributes without Identity
            IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('Attributes'))
            BEGIN
                DROP TABLE Attributes;
                CREATE TABLE Attributes ( 
                    [Id] INT NOT NULL PRIMARY KEY, 
                    [ArabicName] NVARCHAR(255) NOT NULL, 
                    [EnglishName] NVARCHAR(255) NOT NULL 
                );
            END

            -- 4. Drop and Recreate MenuSalesItems without Identity
            IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('MenuSalesItems'))
            BEGIN
                DROP TABLE MenuSalesItems;
                CREATE TABLE MenuSalesItems ( 
                    [Id] INT NOT NULL PRIMARY KEY, 
                    [ArabicName] NVARCHAR(70) NOT NULL, 
                    [EnglishName] NVARCHAR(70) NOT NULL, 
                    [NormalizedEnglishName] NVARCHAR(70) NOT NULL, 
                    [Price] DECIMAL(18,2) NULL, 
                    [CategoryId] INT NULL, 
                    [Barcode] NVARCHAR(MAX) NULL, 
                    [Invisible] BIT NOT NULL DEFAULT 0, 
                    [CreationDate] DATETIME2 NOT NULL, 
                    [UpdatedDate] DATETIME2 NULL, 
                    [BranchId] INT NULL, 
                    [AttributeId] INT NULL, 
                    [AttributePrice] DECIMAL(18,2) NULL, 
                    [HasAttribute] BIT NOT NULL DEFAULT 0, 
                    [Description] NVARCHAR(255) NULL, 
                    [ImagePath] NVARCHAR(255) NULL, 
                    [BackColor] NVARCHAR(7) NULL, 
                    [TextColor] NVARCHAR(7) NULL, 
                    [TextSize] INT NULL, 
                    [FirstPrice] DECIMAL(18,2) NULL, 
                    [SecondPrice] DECIMAL(18,2) NULL, 
                    [ThirdPrice] DECIMAL(18,2) NULL, 
                    [FourthPrice] DECIMAL(18,2) NULL, 
                    [FifthPrice] DECIMAL(18,2) NULL, 
                    [Tax] DECIMAL(18,2) NULL, 
                    [PrintInBackupReceipt] BIT NOT NULL DEFAULT 1, 
                    [KitchenTypeId] INT NULL, 
                    [MainCategoryId] INT NULL, 
                    [ByWeight] BIT NOT NULL DEFAULT 0 
                );
            END

            -- 5. Drop and Recreate AttributeItems without Identity
            IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('AttributeItems'))
            BEGIN
                DROP TABLE AttributeItems;
                CREATE TABLE AttributeItems ( 
                    [Id] INT NOT NULL PRIMARY KEY, 
                    [AppearanceIndex] INT NOT NULL, 
                    [AttributeId] INT NOT NULL, 
                    [RelatedMenuItemId] INT NOT NULL 
                );
            END

            -- 6. Restore FKs
            IF OBJECT_ID('Categories') IS NOT NULL AND OBJECT_ID('MenuSalesItems') IS NOT NULL
                ALTER TABLE MenuSalesItems ADD CONSTRAINT FK_MenuSalesItems_Categories_CategoryId FOREIGN KEY ([CategoryId]) REFERENCES Categories([Id]) ON DELETE SET NULL;
            IF OBJECT_ID('Attributes') IS NOT NULL AND OBJECT_ID('AttributeItems') IS NOT NULL
                ALTER TABLE AttributeItems ADD CONSTRAINT FK_AttributeItems_Attributes_AttributeId FOREIGN KEY ([AttributeId]) REFERENCES Attributes([Id]) ON DELETE CASCADE;
            IF OBJECT_ID('MenuSalesItems') IS NOT NULL AND OBJECT_ID('AttributeItems') IS NOT NULL
                ALTER TABLE AttributeItems ADD CONSTRAINT FK_AttributeItems_MenuSalesItems_RelatedMenuItemId FOREIGN KEY ([RelatedMenuItemId]) REFERENCES MenuSalesItems([Id]) ON DELETE CASCADE;
            IF OBJECT_ID('OrderItemAttributes') IS NOT NULL AND OBJECT_ID('AttributeItems') IS NOT NULL
                ALTER TABLE OrderItemAttributes ADD CONSTRAINT FK_OrderItemAttributes_AttributeItems_AttributeItemId FOREIGN KEY ([AttributeItemId]) REFERENCES AttributeItems([Id]) ON DELETE SET NULL;
            IF OBJECT_ID('MenuSalesItems') IS NOT NULL AND OBJECT_ID('ItemsClassifications') IS NOT NULL
                ALTER TABLE MenuSalesItems ADD CONSTRAINT FK_MenuSalesItems_ItemsClassifications_MainCategoryId FOREIGN KEY ([MainCategoryId]) REFERENCES ItemsClassifications([Id]) ON DELETE SET NULL;
        END
    ");

    // Hotfix: Ensure StaffMealGroups table schema is correct
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('StaffMealGroups') IS NOT NULL
        BEGIN
            IF COL_LENGTH('StaffMealGroups', 'DailyLimit') IS NULL
                ALTER TABLE StaffMealGroups ADD DailyLimit INT NOT NULL DEFAULT 1;
            IF COL_LENGTH('StaffMealGroups', 'MealLimit') IS NULL
                ALTER TABLE StaffMealGroups ADD MealLimit INT NOT NULL DEFAULT 1;
            IF COL_LENGTH('StaffMealGroups', 'DailyAmountLimit') IS NULL
                ALTER TABLE StaffMealGroups ADD DailyAmountLimit DECIMAL(18,2) NOT NULL DEFAULT 0;
            -- Added safety for DailyLimit and MealLimit if missing from earlier migrations
        END

        IF OBJECT_ID('StaffMealConfigs') IS NOT NULL
        BEGIN
            IF COL_LENGTH('StaffMealConfigs', 'DailyLimit') IS NULL
                ALTER TABLE StaffMealConfigs ADD DailyLimit INT NOT NULL DEFAULT 1;
            IF COL_LENGTH('StaffMealConfigs', 'MealLimit') IS NULL
                ALTER TABLE StaffMealConfigs ADD MealLimit INT NOT NULL DEFAULT 1;
            IF COL_LENGTH('StaffMealConfigs', 'DailyAmountLimit') IS NULL
                ALTER TABLE StaffMealConfigs ADD DailyAmountLimit DECIMAL(18,2) NOT NULL DEFAULT 0;
        END
    ");


    await PosDbContextDataSeed.SeedAsync(dbContext);
    await AppIdentityDbContextSeed.SeedAsync(userManager, roleManager, identityDbContext);
}
catch (Exception ex)
{
    var logger = loggerFactory.CreateLogger<Program>();
    logger.LogError(ex, "An error occurred during migration");
}

#endregion

#region Configure Kestrel Middelewares

app.UseMiddleware<ExeptionMiddleWare>();

app.UseSwaggerServices();

app.UseStatusCodePagesWithReExecute("/errors/{0}");

// app.UseHttpsRedirection();

app.UseCors("MyPolicy");
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DeliveryHub>("/callcenterhub");
#endregion

await app.RunAsync();
