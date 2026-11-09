namespace Pos.Repository.Data.DataSeed;

public static class PosDbContextDataSeed
{
    private static readonly List<string> potentialFilePaths =
    [
        Path.Combine("Data", "DataSeed","JsonFiles"),
        Path.Combine("..", "Pos.Repository", "Data", "DataSeed","JsonFiles"),
        Path.Combine("f:", "PointOfSaleProject", "src", "Pos.Repository", "Data", "DataSeed","JsonFiles"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DataSeed","JsonFiles"),
    ];

    public async static Task SeedAsync(AppDbContext _dbContext)
    {
        if (!_dbContext.Companies.Any())
        {
            var companies = await GetDataFromJsonFile<Company>("company.json");
            if (companies != null && companies.Any())
            {
                companies[0].CreationDate = DateTime.Now;
                await _dbContext.Companies.AddAsync(companies[0]);
                await _dbContext.SaveChangesAsync();
            }
        }

        Company? company = await _dbContext.Companies.FirstOrDefaultAsync();

        if (!_dbContext.Branches.Any())
        {
            var branches = await GetDataFromJsonFile<Branch>("branch.json");
            if (branches != null && branches.Any())
            {
                branches[0].CreationDate = DateTime.Now;
                if (company != null)
                {
                    branches[0].CompanyId = company.Id;
                }
                await _dbContext.Branches.AddAsync(branches[0]);
                await _dbContext.SaveChangesAsync();
            }
        }

        Branch? branch = await _dbContext.Branches.FirstOrDefaultAsync();

        if(!_dbContext.AppDate.Any() && branch != null)
        {
            var appDate = new AppDate()
            {
                BranchId = branch.Id,
                PosDate = DateTime.Now.Date,
                StoreDate = DateTime.Now.Date,
               CurrentOrderNumber = 0
            };
            await _dbContext.AppDate.AddAsync(appDate);
            await _dbContext.SaveChangesAsync();
        }

        if (!_dbContext.KitchenTypes.Any())
        {
            var kitchenTypes = await GetDataFromJsonFile<KitchenType>("kitchenTypes.json");
            if (branch != null)
            {
                foreach (var kitchenType in kitchenTypes)
                    kitchenType.BranchId = branch!.Id;
            }
            await _dbContext.KitchenTypes.AddRangeAsync(kitchenTypes);
            await _dbContext.SaveChangesAsync();
        }

        if (!_dbContext.KitchenPrinters.Any())
        {
            var kitchenPrinters = await GetDataFromJsonFile<KitchenPrinters>("kitchenPrinters.json");
            if (kitchenPrinters != null && kitchenPrinters.Any())
            {
                await _dbContext.KitchenPrinters.AddRangeAsync(kitchenPrinters);
                await _dbContext.SaveChangesAsync();
            }
        }

        if (!_dbContext.OrderSettings.Any())
        {
            var orderSettings = await GetDataFromJsonFile<OrderSetting>("orderSettings.json");
            if (branch != null)
            {
                foreach (var orderSetting in orderSettings)
                    orderSetting.BranchID = branch!.Id;
            }
            await _dbContext.OrderSettings.AddRangeAsync(orderSettings);
            await _dbContext.SaveChangesAsync();
        }


        if (!_dbContext.TableGroups.Any())
        {
            var tableGroups = await GetDataFromJsonFile<TableGroup>("TableGroups.json");
            if (branch != null)
            {
                foreach (var tableGroup in tableGroups)
                {
                    tableGroup.BranchID = branch!.Id;
                    tableGroup.CreationDate = DateTime.Now;
                }
            }
            await _dbContext.TableGroups.AddRangeAsync(tableGroups);
            await _dbContext.SaveChangesAsync();
        }

        if (!_dbContext.Tables.Any() && _dbContext.TableGroups.Any())
        {
            var tables = await GetDataFromJsonFile<Table>("Tables.json");
            if (branch != null)
            {
                foreach (var table in tables)
                    table.BranchID = branch!.Id;
            }
            await _dbContext.Tables.AddRangeAsync(tables);
            await _dbContext.SaveChangesAsync();
        }

        if(!_dbContext.DeliveryCustomerTitle.Any())
        {
            var deliveryCustomerTitles = await GetDataFromJsonFile<DeliveryCustomerTitle>("DeliveryCustomerTitle.json");
            
            await _dbContext.DeliveryCustomerTitle.AddRangeAsync(deliveryCustomerTitles);
            await _dbContext.SaveChangesAsync();
        }

        if (!_dbContext.ItemsClassifications.Any())
        {
            var classifications = await GetDataFromJsonFile<ItemsClassifications>("ItemsClassifications.json");
            await _dbContext.ItemsClassifications.AddRangeAsync(classifications);
            await _dbContext.SaveChangesAsync();
        }


        if (!_dbContext.PaymentMethods.Any())
        {
            var paymentMethods = await GetDataFromJsonFile<POS.Core.Entities.Payment.PaymentMethodEntity>("paymentMethods.json");
            if (paymentMethods != null && paymentMethods.Any())
            {
                await _dbContext.PaymentMethods.AddRangeAsync(paymentMethods);
                await _dbContext.SaveChangesAsync();
            }
        }

        if (!_dbContext.DispatcherSettings.Any())
        {
            var dispatcherSettings = new POS.Core.Entities.Settings.DispatcherSetting
            {
                ComputerName = Environment.MachineName,
                IsDispatcher = false,
                RefreshTimeForDeliveryOrderColorsPerSecond = 30,
                CriticalTimeForDeliveryOrderPerMinute = 60,
                WarningTimeForDeliveryOrderPerMinute = 45,
                VoidLimitMinutesForDeliveryOrder = 15,
                AllowVoidLimitMinutesForDeliveryOrder = false,
                AllowDeliveryVoidFromBranch = true
            };
            await _dbContext.DispatcherSettings.AddAsync(dispatcherSettings);
            await _dbContext.SaveChangesAsync();
        }

                await SeedOrRepairUnitsAsync(_dbContext);

        if (!_dbContext.ItemTypes.Any())
        {
            var itemTypes = await GetDataFromJsonFile<ItemType>("itemTypes.json");
            if (itemTypes != null && itemTypes.Any())
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ItemTypes ON");
                    await _dbContext.ItemTypes.AddRangeAsync(itemTypes);
                    await _dbContext.SaveChangesAsync();
                    await _dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT ItemTypes OFF");
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }

    private static string FindValidFilePath(List<string> paths, string fileName)
    {
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return string.Empty;
    }

    public static async Task<List<T>> GetDataFromJsonFile<T>(string fileName)
    {
        var filePath = FindValidFilePath(potentialFilePaths, fileName);
        if (string.IsNullOrEmpty(filePath))
            return [];

        var data = await File.ReadAllTextAsync(filePath);
        var options = new JsonSerializerOptions { AllowTrailingCommas = true, PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<T>>(data, options) ?? [];
    }

    private static async Task SeedOrRepairUnitsAsync(AppDbContext db)
    {
        var standardUnits = new (string ArabicName, string EnglishName, string Code, bool IsBase)[]
        {
            ("كيلو جرام", "Kilogram", "kg", true),
            ("جرام", "Gram", "g", false),
            ("ملي جرام", "Milligram", "mg", false),
            ("طن", "Ton", "ton", false),
            ("لتر", "Liter", "L", true),
            ("ملي لتر", "Milliliter", "ml", false),
            ("جالون", "Gallon", "gal", false),
            ("برميل", "Barrel", "bbl", false),
            ("قطعة", "Piece", "pcs", true),
            ("كرتونة", "Carton", "ctn", false),
            ("علبة", "Box", "box", false),
            ("شيكارة / كيس", "Bag", "bag", false),
            ("صفيحة / كان", "Tin/Can", "tin", false),
            ("رول", "Roll", "roll", false),
            ("دستة", "Dozen", "doz", false),
            ("بالتة", "Pallet", "plt", false),
            ("قفص / صندوق", "Crate", "crt", false),
            ("زجاجة", "Bottle", "btl", false),
            ("برطمان", "Jar", "jar", false),
            ("كيس تفريغ", "Pouch", "pch", false),
            ("حزمة", "Bundle", "bdl", false),
            ("قالب", "Block/Bar", "bar", false),
            ("متر", "Meter", "m", true),
            ("سنتيمتر", "Centimeter", "cm", false),
            ("باكت", "Pack", "pk", false)
        };

        // Sync Branch Units
        var existingUnits = await db.Units.ToListAsync();
        foreach (var u in standardUnits)
        {
            var match = existingUnits.FirstOrDefault(x => x.Code == u.Code || x.EnglishName == u.EnglishName);
            if (match != null)
            {
                if (string.IsNullOrWhiteSpace(match.ArabicName) || match.ArabicName.Contains("?") || match.ArabicName.Contains("Ù") || match.ArabicName != u.ArabicName)
                {
                    match.ArabicName = u.ArabicName;
                    match.EnglishName = u.EnglishName;
                    match.Code = u.Code;
                    match.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                await db.Units.AddAsync(new Unit
                {
                    ArabicName = u.ArabicName,
                    EnglishName = u.EnglishName,
                    Code = u.Code,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
