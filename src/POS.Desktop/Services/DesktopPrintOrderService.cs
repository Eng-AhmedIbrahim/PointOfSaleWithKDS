using POS.Core.Services.Contract;
using BlazorBase.ERPFrontServices.ItemServices;

namespace POS.Desktop.Services;

public class DesktopPrintOrderService : IPrintOrderService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastVoidPrints = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (int? kitchenTypeId, int? catKitchenTypeId, bool? backupItem, bool? backupCat)> _itemKitchenCache = new();
    private readonly CommonProperties _commonProperties;
    private readonly IOrderSettingsService _orderSettingsService;
    private readonly IPrinterServices _printerServices;
    private readonly IBranchService _branchService;
    private readonly IStaffMealService _staffMealService;
    private readonly ILocalStorageService _localStorage;
    private readonly IItemService? _itemService;

    private string LogoPath
    {
        get
        {
            try
            {
                var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "ReceiptLogo");
                if (!Directory.Exists(folderPath))
                    folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "ReceiptLogo");

                if (Directory.Exists(folderPath))
                {
                    var file = Directory.GetFiles(folderPath, "*.*")
                        .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    return file ?? "";
                }
            }
            catch { }
            return "";
        }
    }

    public DesktopPrintOrderService(
        CommonProperties commonProperties,
        IOrderSettingsService orderSettingsService,
        IPrinterServices printerServices,
        IBranchService branchService,
        IStaffMealService staffMealService,
        IConfiguration configuration,
        Blazored.LocalStorage.ILocalStorageService localStorage,
        IItemService? itemService = null)
    {
        _commonProperties = commonProperties;
        _orderSettingsService = orderSettingsService;
        _printerServices = printerServices;
        _branchService = branchService;
        _staffMealService = staffMealService;
        _localStorage = localStorage;
        _itemService = itemService;
    }

    public async Task PrintDineInClosingReceipt(DineInOrderDetails order)
    {
        try
        {
            await PrintDineInLocally(order, isClosing: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in PrintDineInClosingReceipt for order {OrderId}", order.BasicOrderDetails?.OrderId);
        }
    }

    public async Task PrintInitialDineInOrder(DineInOrderDetails order,
        bool printCustomer = true,
        bool printKitchen = true,
        bool isClosing = false,
        bool isUpdate = false,
        bool isPrePrint = false,
        string? printerName = null,
        bool isCopy = false)
    {
        try
        {
            if (printCustomer)
                await PrintDineInLocally(order, isClosing, isUpdate, printerName: printerName);

            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "DineIn");
            if (orderSettings != null && printKitchen)
            {
                var orderDto = new OrderDto
                {
                    Id = order.BasicOrderDetails!.Id,
                    OrderId = order.BasicOrderDetails!.OrderId,
                    CashierName = order.BasicOrderDetails.CashierName,
                    OrderType = "DineIn",
                    OrderDetails = order.BasicOrderDetails.Items,
                    TableName = order.RelatedTableName,
                    OrderNotice = _commonProperties.OrderNote,
                    OrderSettings = _commonProperties.OrderSettings,
                    DiscountReason = _commonProperties.OrderDiscount?.DiscountReason,
                    DiscountType = _commonProperties.OrderDiscount?.DiscountType,
                    DiscountPercentage = _commonProperties.OrderDiscount?.Percentage ?? 0,
                    TotalOrderDiscount = _commonProperties.TotalDiscount,
                    TotalDiscount = _commonProperties.TotalDiscount
                };

                Log.Information("PrintInitialDineInOrder: Processing {Count} items for kitchen printing", order.BasicOrderDetails.Items?.Count ?? 0);
                if (order.BasicOrderDetails.Items != null)
                {
                    foreach (var item in order.BasicOrderDetails.Items)
                    {
                        Log.Information("  Item: {Name}, ItemKitchenId: {ItemId}, CatKitchenId: {CatId}, PrintBackupFromItem: {BackupItem}, PrintBackupFromCat: {BackupCat}",
                            item.Name, item.ItemKitchenTypeId, item.CategoryKitchenTypeId,
                            item.PrintInBackupReceiptFromItem, item.PrintInBackupReceiptFromCategory);
                    }
                }

                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(orderDto, orderDto, isReprint: isClosing, isCopy: isCopy, targetPrinterName: printerName);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(orderDto, orderDto, isReprint: isClosing, isCopy: isCopy, targetPrinterName: printerName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print dine-in order locally");
        }
    }

    private async Task PrintDineInLocally(DineInOrderDetails order,
        bool isClosing = false, bool isUpdate = false, string? printerName = null)
    {
        try
        {
            var branch = await _branchService.GetBranches();
            var currentBranch = branch.ElementAtOrDefault(0);
            var firstBranch = branch.ElementAtOrDefault(0);
            var receiptName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name : firstBranch?.Name;
            if (string.IsNullOrWhiteSpace(receiptName) || receiptName.Equals("Store", StringComparison.OrdinalIgnoreCase) || receiptName.Equals("Store Name", StringComparison.OrdinalIgnoreCase))
                receiptName = "";

            var receipt = new DineInReceipt()
            {
                Id = order.BasicOrderDetails!.OrderId,
                StoreName = receiptName,
                CashierName = order.BasicOrderDetails.CashierName ?? "Cashier",
                CaptainName = order.CaptainName ?? "Captain",
                ReceiptType = "DineIn",
                DateCreated = order.BasicOrderDetails?.OrderDataTime ?? DateTime.Now,
                PaymentMethod = "Cash",
                FooterMessage = _commonProperties.DineInSettings?.OrderStatment ?? "",
                LogoPath = LogoPath,
                LogoWidth = currentBranch?.LogoWidth ?? 150,
                TotalAmount = order.BasicOrderDetails!.Account,
                ServiceAmount = order.BasicOrderDetails.Service?.ToString("0.##") ?? "0.00",
                TaxAmount = order.BasicOrderDetails.Tax?.ToString("0.##") ?? "0.00",
                Discount = (order.BasicOrderDetails.Items?.Sum(i => i.TotalDiscountPrice ?? 0) ?? 0) +
                           (order.BasicOrderDetails.OrderDiscount?.Value ?? 0),
                TotalOrder = order.BasicOrderDetails.Total?.ToString("0.##") ?? "0.00",
                IsVoid = order.BasicOrderDetails.IsVoided ?? false,
                IsClosing = isClosing,
                IsHospitality = order.BasicOrderDetails.IsHospitality,
                HospitalityResponsibleName = order.BasicOrderDetails.HospitalityResponsibleName,
                HospitalityReason = order.BasicOrderDetails.HospitalityReason,
                IsStaffMeal = order.BasicOrderDetails.IsStaffMeal,
                StaffMealEmployeeName = order.BasicOrderDetails.StaffMealEmployeeName
            };

            var items = order.BasicOrderDetails.Items ?? new List<TableItem>();
            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "DineIn");

            int countToPrint = 0;
            if (orderSettings != null)
            {
                countToPrint = isClosing
                    ? (orderSettings.ClosingReceiptCount ?? 0)
                    : (orderSettings.CustomerReceiptCount ?? 0);
            }

            Log.Information("PrintDineInLocally started. isClosing: {isClosing}, countToPrint: {countToPrint}", isClosing, countToPrint);

            int currentPrintCount = 0;
            if (!isUpdate)
            {
                try
                {
                    currentPrintCount = await _orderSettingsService.IncrementPrintCountAsync(order.DatabaseId);
                    order.PrintCount = currentPrintCount; // Update the order object as well
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error incrementing print count for DineIn order {DatabaseId}", order.DatabaseId);
                }
            }
            else
            {
                currentPrintCount = order?.PrintCount ?? 0; // Use existing count for updates
            }

            receipt.PrintCount = currentPrintCount;

            if (countToPrint > 0)
            {
                var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
                Log.Information("Fetched {Count} kitchens", kitchens?.Count ?? 0);

                var customerKitchen = kitchens?.ElementAtOrDefault(0);
                if (customerKitchen != null)
                {
                    var printers = customerKitchen.KitchenPrinters?.FirstOrDefault();
                    if (printers != null)
                    {
                        string? originalPath = null;
                        string? copyPath = null;
                        var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                        Directory.CreateDirectory(reportsFolder);

                        for (int i = 1; i <= countToPrint; i++)
                        {
                            var currentPrinterName = printerName ?? (typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string);
                            Log.Information("Iteration {i}: printerName resolved to '{currentPrinterName}'", i, currentPrinterName);

                            if (string.IsNullOrEmpty(currentPrinterName))
                            {
                                // Skip fallback for secondary copies to prevent unwanted duplication
                                if (i > 1 && string.IsNullOrEmpty(printerName))
                                {
                                    continue;
                                }

                                currentPrinterName = await _localStorage.GetItemAsStringAsync("cashier_printer_name");
                                if (string.IsNullOrEmpty(currentPrinterName))
                                {
                                    currentPrinterName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                                    Log.Information("No kitchen printer or stored cashier printer. Using system default: {Printer}", currentPrinterName);
                                }
                            }

                            if (string.IsNullOrEmpty(currentPrinterName)) continue;

                            bool isActuallyCopy = !isClosing && ((currentPrintCount > 1) || (i > 1));

                            string pathToPrint;
                            if (isActuallyCopy)
                            {
                                if (copyPath == null)
                                {
                                    receipt.IsCopy = true;
                                    var document = new DineInReceiptDocument(receipt, items);
                                    copyPath = Path.Combine(reportsFolder, $"Order_{order.BasicOrderDetails!.OrderId}_DineIn_Copy_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                    Log.Information("Generating COPY PDF: {Path}", copyPath);
                                    document.GeneratePdf(copyPath);
                                    if (!File.Exists(copyPath)) Log.Error("Failed to generate COPY PDF at {Path}", copyPath);
                                }
                                pathToPrint = copyPath;
                            }
                            else
                            {
                                if (originalPath == null)
                                {
                                    receipt.IsCopy = false;
                                    var document = new DineInReceiptDocument(receipt, items);
                                    originalPath = Path.Combine(reportsFolder, $"Order_{order.BasicOrderDetails!.OrderId}_DineIn_Original_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                    Log.Information("Generating ORIGINAL PDF: {Path}", originalPath);
                                    document.GeneratePdf(originalPath);
                                    if (!File.Exists(originalPath)) Log.Error("Failed to generate ORIGINAL PDF at {Path}", originalPath);
                                }
                                pathToPrint = originalPath;
                            }

                            try
                            {
                                Log.Information("Sending PDF to printer '{PrinterName}': {Path}", currentPrinterName, pathToPrint);
                                var printSuccess = await _printerServices.PrintPdfAsync(pathToPrint, currentPrinterName);
                                Log.Information("PrintPdfAsync result: {Success} for printer {PrinterName}", printSuccess, currentPrinterName);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error printing to printer {PrinterName}", currentPrinterName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print dine-in locally for order {OrderId}", order.BasicOrderDetails?.OrderId);
        }
    }

    public async Task<bool> PrintTakeAwayOrder(decimal paid = 0,
        string customerName = "",
        string customerPhone = "",
        PaymentMethod paymentMethod = PaymentMethod.Cash)
    {
        // 1. Check if Staff Mode is active
        if (_commonProperties.CurrentStaffMeal != null)
        {
             var staffMealConfig = _commonProperties.CurrentStaffMeal;
             var items = _commonProperties.TableItems?.ToList() ?? new List<TableItem>();
             
             var staffResult = await PrintStaffMealOrder(staffMealConfig.UserName ?? "Employee", items);
             
             if (staffResult)
             {
                 // Clear staff mode after successful print
                 _commonProperties.ClearStaffMeal();
                 _commonProperties.TableItems?.Clear();
                 _commonProperties.NotifyStateChanged();
             }
             return staffResult;
        }

        // 2. Check if Hospitality Mode is active
        if (_commonProperties.IsHospitalityMode)
        {
            var hospResult = await PrintHospitalityOrder(_commonProperties.HospitalityResponsiblePerson!, _commonProperties.HospitalityReason!);
            if (hospResult)
            {
                _commonProperties.ClearHospitality();
                _commonProperties.TableItems?.Clear();
                _commonProperties.NotifyStateChanged();
            }
            return hospResult;
        }

        BackupMainOrderDtoDetails(customerName, customerPhone, paid, paymentMethod);

        _commonProperties.OrderDto!.SkipPrintingOnServer = true;
        var result = await _orderSettingsService.CreateOrderAsync(_commonProperties.OrderDto!);

        if (result is null)
            return false;

        _commonProperties.CurrentOrderId = result.OrderId + 1;
        if (result.OrderDate.HasValue)
            _commonProperties.PosDate = DateOnly.FromDateTime(result.OrderDate.Value);

        try
        {
            await PrintTakeAwayLocally(_commonProperties.OrderDto!, result);

            var orderSettings = _commonProperties.OrderDto.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(_commonProperties.OrderDto!, result, isReprint: true);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(_commonProperties.OrderDto!, result, isReprint: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print takeaway order locally");
        }

        return true;
    }

    public async Task<bool> ReprintOrderAsync(int orderId, bool isCopy = false, bool printCustomer = true, bool printKitchen = true, string? printerName = null)
    {
        try
        {
            var order = await _orderSettingsService.GetOrderByIdAsync(orderId);
            if (order == null) return false;

            if (order.OrderSettings == null || !order.OrderSettings.Any())
            {
                order.OrderSettings = _commonProperties.OrderSettings;
            }

            if (order.OrderState == "Voided")
            {
                isCopy = false; // Never treat a voided order reprint as a simple copy!
            }

            if (order.OrderType == "DineIn")
            {
                var dineInOrderDetails = new DineInOrderDetails
                {
                    BasicOrderDetails = new BlazorBase.Models.OrderDetails
                    {
                        OrderId = order.OrderId,
                        CashierName = order.CashierName,
                        Items = order.OrderDetails ?? new List<TableItem>(),
                        OrderDataTime = order.OrderDate ?? DateTime.Now,
                        Total = order.GrandTotal ?? 0,
                        Service = order.Services ?? 0,
                        Tax = order.Tax ?? 0,
                        Account = order.SubTotal ?? 0,
                        OrderDiscount = new OrderDiscount
                        {
                            Value = order.TotalDiscount ?? 0,
                            Percentage = order.DiscountPercentage ?? 0
                        },
                        IsVoided = order.OrderState == "Voided"
                    },
                    CaptainName = order.WaiterName ?? "Unknown",
                    RelatedTableId = order.TableId,
                    RelatedTableName = order.TableName
                };

                await PrintInitialDineInOrder(dineInOrderDetails, printCustomer: printCustomer, printKitchen: printKitchen, isClosing: false, isUpdate: true, isPrePrint: false, printerName: printerName, isCopy: isCopy);
            }
            else if (order.OrderType == "Delivery")
            {
                if (printCustomer)
                    await PrintDeliveryLocally(order, order, isFollowUp: false, parentDeliveryFees: null, isCopy: isCopy, printerName: printerName);

                var orderSettings = order.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");
                if (orderSettings != null && printKitchen)
                {
                    var rCount = orderSettings.FullKitchenReceiptCount > 0 ? orderSettings.FullKitchenReceiptCount : 1;
                    await PrintBackupReceiptsLocally(order, order, isReprint: true, isCopy: isCopy, reprintCount: rCount, targetPrinterName: printerName);

                    var sCount = orderSettings.SeparateReceiptCount > 0 ? orderSettings.SeparateReceiptCount : 1;
                    await PrintKitchenReceiptsLocally(order, order, isReprint: true, isCopy: isCopy, reprintCount: sCount, targetPrinterName: printerName);
                }
            }
            else
            {
                if (printCustomer)
                    await PrintTakeAwayLocally(order, order, isCopy: isCopy, printerName: printerName);

                var orderSettings = order.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
                if (orderSettings != null && printKitchen)
                {
                    var rCount = orderSettings.FullKitchenReceiptCount > 0 ? orderSettings.FullKitchenReceiptCount : 1;
                    await PrintBackupReceiptsLocally(order, order, isReprint: true, isCopy: isCopy, reprintCount: rCount, targetPrinterName: printerName);

                    var sCount = orderSettings.SeparateReceiptCount > 0 ? orderSettings.SeparateReceiptCount : 1;
                    await PrintKitchenReceiptsLocally(order, order, isReprint: true, isCopy: isCopy, reprintCount: sCount, targetPrinterName: printerName);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reprint order {OrderId}", orderId);
            return false;
        }
    }

    private void BackupMainOrderDtoDetails(string customerName,
        string customerPhone,
        decimal paid = 0,
        PaymentMethod paymentMethod = PaymentMethod.Cash)
    {
        _commonProperties.OrderDto!.OrderId = _commonProperties.CurrentOrderId;
        _commonProperties.OrderDto!.OrderType = "TakeAway";
        _commonProperties.OrderDto.CashierName = _commonProperties.CurrentUser;

        // Ensure Branch Details are populated
        if (_commonProperties.BranchDetails == null || string.IsNullOrWhiteSpace(_commonProperties.BranchDetails.Name))
        {
            // Try to use common properties store name or default
            _commonProperties.OrderDto.BranchName = _commonProperties.StoreName;
        }
        else
        {
            _commonProperties.OrderDto.BranchId = _commonProperties.BranchDetails.Id;
            _commonProperties.OrderDto.BranchName = _commonProperties.BranchDetails.Name;
        }

        _commonProperties.OrderDto.CashierId = _commonProperties.CurrentUserId;
        _commonProperties.OrderDto.FooterMessage = _commonProperties.TakeAwaySettings!.OrderStatment;
        _commonProperties.OrderDto.PaymentMethod = paymentMethod;
        _commonProperties.OrderDto.Paid = paid > 0.00m ? paid : (_commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 4 ? _commonProperties._financeSettingsList[4].Value : 0);
        _commonProperties.OrderDto.SubTotal = _commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 0 ? _commonProperties._financeSettingsList[0].Value : 0;
        _commonProperties.OrderDto.Services = _commonProperties._financeSettingsList?.Count > 3 ? _commonProperties._financeSettingsList[3].Value : 0M;
        _commonProperties.OrderDto.Tax = _commonProperties._financeSettingsList?.Count > 2 ? _commonProperties._financeSettingsList[2].Value : 0M;
        _commonProperties.OrderDto.TotalOrderDiscount = _commonProperties.TotalDiscount;
        _commonProperties.OrderDto.TotalDiscount = _commonProperties.TotalDiscount;
        _commonProperties.OrderDto.DiscountPercentage = _commonProperties.OrderDiscount?.Percentage ?? 0;
        _commonProperties.OrderDto.DiscountReason = _commonProperties.OrderDiscount?.DiscountReason;
        _commonProperties.OrderDto.DiscountType = _commonProperties.OrderDiscount?.DiscountType;
        _commonProperties.OrderDto.DiscountByName = _commonProperties.CurrentUser;
        _commonProperties.OrderDto.DiscountTime = DateTime.Now;
        _commonProperties.OrderDto.GrandTotal = _commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 4 ? _commonProperties._financeSettingsList[4].Value : 0;
        _commonProperties.OrderDto.OrderDate = DateTime.Now;
        _commonProperties.OrderDto.OrderState = "Completed";
        _commonProperties.OrderDto.OrderNotice = _commonProperties.OrderNote;
        _commonProperties.OrderDto.OrderDetails = _commonProperties.TableItems;
        _commonProperties.OrderDto.TakeAwayCustomerName = customerName;
        _commonProperties.OrderDto.TakeawayCustomerPhone = customerPhone;
        _commonProperties.OrderDto.CustomerName = customerName;
        _commonProperties.OrderDto.CustomerPhone = customerPhone;
        _commonProperties.OrderDto.OrderSettings = _commonProperties.OrderSettings;
        _commonProperties.OrderDto.MachineName = Environment.MachineName;

        // Hospitality & Staff Meals (Normal mode but might have flags)
        _commonProperties.OrderDto.IsHospitality = _commonProperties.IsHospitalityMode;
        _commonProperties.OrderDto.HospitalityResponsibleName = _commonProperties.HospitalityResponsiblePerson;
        _commonProperties.OrderDto.HospitalityReason = _commonProperties.HospitalityReason;
        _commonProperties.OrderDto.IsStaffMeal = _commonProperties.CurrentStaffMeal != null;
        _commonProperties.OrderDto.StaffMealEmployeeName = _commonProperties.CurrentStaffMeal?.UserName;
    }

    private async Task PrintTakeAwayLocally(OrderDto orderDto, OrderDto createdOrder, bool isCopy = false, string? printerName = null)
    {
        try
        {
            var branch = await _branchService.GetBranches();
            var currentBranch = branch.ElementAtOrDefault(0);
            var firstBranch = branch.ElementAtOrDefault(0);
            var receiptName = !string.IsNullOrWhiteSpace(orderDto.BranchName) ? orderDto.BranchName : firstBranch?.Name;
            if (string.IsNullOrWhiteSpace(receiptName) || receiptName.Equals("Store", StringComparison.OrdinalIgnoreCase) || receiptName.Equals("Store Name", StringComparison.OrdinalIgnoreCase))
                receiptName = "";

            var receipt = new Receipt()
            {
                Id = createdOrder.OrderId,
                StoreName = receiptName,
                CashierName = orderDto.CashierName ?? "Cashier",
                ReceiptType = orderDto.OrderType ?? "TakeAway",
                DateCreated = createdOrder.OrderDate ?? DateTime.Now,
                PaymentMethod = orderDto.PaymentMethod.ToString() ?? "Cash",
                FooterMessage = orderDto.FooterMessage ?? "",
                LogoPath = LogoPath,
                LogoWidth = currentBranch?.LogoWidth ?? 150,
                TotalAmount = orderDto.GrandTotal,
                Discount = orderDto.TotalOrderDiscount,
                Tax = orderDto.Tax,
                Services = orderDto.Services,
                SubTotal = orderDto.SubTotal,
                Items = orderDto.OrderDetails ?? new List<TableItem>(),
                IsVoid = orderDto.OrderState == "Voided",
                IsHospitality = orderDto.IsHospitality ?? false,
                HospitalityResponsibleName = orderDto.HospitalityResponsibleName,
                HospitalityReason = orderDto.HospitalityReason,
                IsStaffMeal = orderDto.IsStaffMeal ?? false,
                StaffMealEmployeeName = orderDto.StaffMealEmployeeName
            };

            var orderSettings = orderDto.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
            Log.Information("PrintTakeAwayLocally started. orderSettings found: {Found}, count: {Count}", orderSettings != null, orderSettings?.CustomerReceiptCount ?? 0);

            int currentPrintCount = 0;
            try
            {
                if (!isCopy)
                {
                    currentPrintCount = await _orderSettingsService.IncrementPrintCountAsync(createdOrder.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error incrementing print count for TakeAway order {Id}", createdOrder.Id);
            }

            // If orderSettings is null or count is 0, we should still print at least one copy if it's a manual reprint or no settings found
            // FOR HOSPITALITY: Always print at least one customer copy
            int receiptCount = (orderSettings != null && (orderSettings.CustomerReceiptCount ?? 0) > 0) ? orderSettings.CustomerReceiptCount.Value : 1;
            if (orderDto.IsHospitality == true && receiptCount < 1) receiptCount = 1;
            
            var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
            var customerKitchen = kitchens?.ElementAtOrDefault(0);
            var printers = customerKitchen?.KitchenPrinters?.FirstOrDefault();

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);

            string? originalPath = null;
            string? copyPath = null;

            for (int i = 1; i <= receiptCount; i++)
            {
                var targetPrinterName = printerName ?? (printers != null ? typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string : null);

                // FALLBACK: Use selected cashier printer from LocalStorage, otherwise system default
                if (string.IsNullOrWhiteSpace(targetPrinterName))
                {
                    // Skip fallback for secondary copies to prevent unwanted duplication 
                    // unless a printer is explicitly assigned for this copy
                    if (i > 1 && string.IsNullOrWhiteSpace(printerName))
                    {
                        Log.Information("Copy {CopyIndex} has no explicit printer configured. Skipping fallback to avoid duplicate print.", i);
                        continue;
                    }

                    targetPrinterName = await _localStorage.GetItemAsStringAsync("cashier_printer_name");
                    
                    if (string.IsNullOrWhiteSpace(targetPrinterName))
                    {
                        targetPrinterName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                        Log.Information("No kitchen printer or stored cashier printer. Using system default: {Printer}", targetPrinterName);
                    }
                    else
                    {
                        Log.Information("Using stored cashier printer: {Printer}", targetPrinterName);
                    }
                }

                if (string.IsNullOrEmpty(targetPrinterName)) continue;

                bool isActuallyCopy = !receipt.IsVoid && (isCopy || (currentPrintCount > 1) || (i > 1));

                string pathToPrint;
                if (isActuallyCopy)
                {
                    if (copyPath == null)
                    {
                        receipt.IsCopy = true;
                        var document = new ReceiptDocument(receipt, orderDto.OrderDetails ?? new List<TableItem>());
                        copyPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_TakeAway_Copy_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                        document.GeneratePdf(copyPath);
                    }
                    pathToPrint = copyPath;
                }
                else
                {
                    if (originalPath == null)
                    {
                        receipt.IsCopy = false;
                        var document = new ReceiptDocument(receipt, orderDto.OrderDetails ?? new List<TableItem>());
                        originalPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_TakeAway_Original_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                        document.GeneratePdf(originalPath);
                    }
                    pathToPrint = originalPath;
                }

                try
                {
                    Log.Information("Sending PDF to printer '{PrinterName}': {Path}", targetPrinterName, pathToPrint);
                    var printSuccess = await _printerServices.PrintPdfAsync(pathToPrint, targetPrinterName);
                    Log.Information("PrintPdfAsync result for {Type} to {Printer}: {Success}", orderDto.OrderType, targetPrinterName, printSuccess);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error printing {Type} to {Printer}", orderDto.OrderType, targetPrinterName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print takeaway locally for order {OrderId}", createdOrder.OrderId);
        }
    }

    private async Task PrintBackupReceiptsLocally(OrderDto order, OrderDto createdOrder, bool isReprint = false, bool isFollowUp = false, bool isCopy = false, int? reprintCount = null, string? targetPrinterName = null)
    {
        if (order.OrderDetails == null) return;

        var itemsToProcess = isReprint
            ? (createdOrder.OrderDetails?.Any() == true ? createdOrder.OrderDetails : order.OrderDetails)
            : order.OrderDetails.Where(i => i.DatabaseId == 0).ToList();

        if (itemsToProcess.Count == 0)
        {
            Log.Information("PrintBackupReceiptsLocally: No items to process.");
            return;
        }

        // Ensure per-item comments are preserved from in-memory order.OrderDetails
        if (order.OrderDetails != null)
        {
            foreach (var item in itemsToProcess)
            {
                if (string.IsNullOrWhiteSpace(item.LineComment))
                {
                    var source = order.OrderDetails.FirstOrDefault(o => o.Id == item.Id && !string.IsNullOrWhiteSpace(o.LineComment));
                    if (source != null)
                    {
                        item.LineComment = source.LineComment;
                    }
                    else if (item.Attributes?.Any(a => a.Id >= 5000) == true)
                    {
                        item.LineComment = string.Join(", ", item.Attributes.Where(a => a.Id >= 5000).Select(a => a.Name));
                    }
                }
            }
        }

        var filteredItems = itemsToProcess
            .Where(i => i.PrintInBackupReceiptFromItem ?? i.PrintInBackupReceiptFromCategory ?? true)
            .ToList();

        if (filteredItems.Count == 0)
        {
            Log.Information("PrintBackupReceiptsLocally: No filtered items for backup receipt.");
            return;
        }

        var receipt = new KitchenReceipt()
        {
            Id = createdOrder.OrderId,
            CashierName = order.CashierName!,
            OrderType = order.OrderType ?? "TakeAway",
            DateCreated = createdOrder.OrderDate.HasValue ? new DateTimeOffset(createdOrder.OrderDate.Value) : DateTimeOffset.Now,
            Items = filteredItems,
            KitchenNote = order.OrderNotice!,
            KitchenType = "Packup",
            TableName = order.TableName,
            IsFollowUp = isFollowUp,
            IsCopy = isCopy,
            IsArabic = _commonProperties.Language == "ar"
        };

        var document = new KitchenReceiptDocument(receipt, filteredItems);
        var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
        Directory.CreateDirectory(reportsFolder);
        var outputPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_{order.OrderType ?? "Type"}_Backup_{DateTime.Now:yyyyMMddHHmmss}.pdf");

        try
        {
            document.GeneratePdf(outputPath);

            var settingsOrderType = (order.OrderType == "Staff" || order.OrderType == "Hospitality") ? "TakeAway" : order.OrderType;
            var orderSettings = order.OrderSettings?.FirstOrDefault(o => o.OrderType == settingsOrderType)
                ?? _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == settingsOrderType);
            var receiptCount = reprintCount ?? orderSettings?.FullKitchenReceiptCount ?? 0;

            if (receiptCount > 0)
            {
                var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
                var backupKitchen = kitchens?.FirstOrDefault(k => k.KitchenName != null && (k.KitchenName.Equals("Backup", StringComparison.OrdinalIgnoreCase) || k.KitchenName.Equals("Packup", StringComparison.OrdinalIgnoreCase)))
                    ?? kitchens?.ElementAtOrDefault(1)
                    ?? kitchens?.FirstOrDefault();

                if (backupKitchen != null)
                {
                    var printers = backupKitchen.KitchenPrinters?.FirstOrDefault(p => p.DeviceName == Environment.MachineName)
                        ?? backupKitchen.KitchenPrinters?.FirstOrDefault();
                    if (printers != null)
                    {
                        for (int i = 1; i <= receiptCount; i++)
                        {
                            var printerName = targetPrinterName ?? (typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string);
                            if (!string.IsNullOrEmpty(printerName))
                            {
                                Log.Information("Printing backup kitchen ticket to {Printer}", printerName);
                                await _printerServices.PrintPdfAsync(outputPath, printerName);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(targetPrinterName))
                    {
                        Log.Information("Printing backup kitchen ticket to specified printer {Printer}", targetPrinterName);
                        await _printerServices.PrintPdfAsync(outputPath, targetPrinterName);
                    }
                    else
                    {
                        Log.Warning("Backup kitchen {KitchenName} has no printers configured.", backupKitchen.KitchenName);
                    }
                }
                else if (!string.IsNullOrEmpty(targetPrinterName))
                {
                    Log.Information("Printing backup kitchen ticket to specified printer {Printer}", targetPrinterName);
                    await _printerServices.PrintPdfAsync(outputPath, targetPrinterName);
                }
                else
                {
                    Log.Warning("No valid backup kitchen found to print full receipt.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print backup receipt locally");
        }
    }

    private async Task PrintKitchenReceiptsLocally(OrderDto order, OrderDto createdOrder, bool isReprint = false, bool isFollowUp = false, bool isCopy = false, int? reprintCount = null, string? targetPrinterName = null)
    {
        var settingsOrderType = (order.OrderType == "Staff" || order.OrderType == "Hospitality") ? "TakeAway" : order.OrderType;
        var orderSettings = order.OrderSettings?.FirstOrDefault(o => o.OrderType == settingsOrderType)
            ?? _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == settingsOrderType);
        var receiptCount = reprintCount ?? orderSettings?.SeparateReceiptCount ?? 0;

        if (receiptCount <= 0 || order.OrderDetails == null)
        {
            Log.Information("PrintKitchenReceiptsLocally: Skipping. receiptCount: {receiptCount}, hasDetails: {hasDetails}", receiptCount, order.OrderDetails != null);
            return;
        }

        var itemsToProcess = isReprint
            ? order.OrderDetails
            : order.OrderDetails.Where(i => i.DatabaseId == 0).ToList();

        if (itemsToProcess.Count == 0)
        {
            Log.Information("PrintKitchenReceiptsLocally: No items to process (isReprint={isReprint}).", isReprint);
            return;
        }

        foreach (var item in itemsToProcess)
        {
            if (string.IsNullOrWhiteSpace(item.LineComment) && item.Attributes?.Any(a => a.Id >= 5000) == true)
            {
                item.LineComment = string.Join(", ", item.Attributes.Where(a => a.Id >= 5000).Select(a => a.Name));
            }
        }

        var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
        if (kitchens == null || !kitchens.Any())
        {
            Log.Warning("PrintKitchenReceiptsLocally: No kitchens found for device {Device}", Environment.MachineName);
            return;
        }

        await EnrichItemsWithKitchenTypesAsync(itemsToProcess);

        // Group by Kitchen ID
        var groups = itemsToProcess
            .Where(item => item.ItemKitchenTypeId.HasValue || item.CategoryKitchenTypeId.HasValue)
            .GroupBy(item => item.ItemKitchenTypeId ?? item.CategoryKitchenTypeId!.Value)
            .ToList();

        Log.Information("PrintKitchenReceiptsLocally: Found {count} kitchen groups to process.", groups.Count);

        foreach (var group in groups)
        {
            var kitchenId = group.Key;
            var kitchenItems = group.ToList();

            // Find kitchen by ID in memory
            var kitchen = kitchens.FirstOrDefault(k => k.Id == kitchenId);
            if (kitchen == null)
            {
                Log.Warning("PrintKitchenReceiptsLocally: Kitchen ID {Id} not configured for this device. Skipping {Count} items.", kitchenId, kitchenItems.Count);
                continue;
            }

            // Skip Customer and Backup kitchens — they are handled by Customer & Backup print methods and must not be duplicated here
            if (kitchen.KitchenName != null && 
                (kitchen.KitchenName.Equals("Backup", StringComparison.OrdinalIgnoreCase) || 
                 kitchen.KitchenName.Equals("Packup", StringComparison.OrdinalIgnoreCase) || 
                 kitchen.KitchenName.Equals("Customer", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Information("PrintKitchenReceiptsLocally: Skipping default kitchen '{KitchenName}' to prevent duplicate backup print.", kitchen.KitchenName);
                continue;
            }

            var printers = kitchen.KitchenPrinters?.FirstOrDefault(p => p.DeviceName == Environment.MachineName)
                ?? kitchen.KitchenPrinters?.FirstOrDefault();
            if (printers == null && string.IsNullOrEmpty(targetPrinterName))
            {
                Log.Warning("PrintKitchenReceiptsLocally: Kitchen {KitchenName} has no printer mapping.", kitchen.KitchenName);
                continue;
            }

            var receipt = new KitchenReceipt()
            {
                Id = createdOrder.OrderId,
                CashierName = order.CashierName!,
                OrderType = order.OrderType ?? "TakeAway",
                DateCreated = createdOrder.OrderDate.HasValue ? new DateTimeOffset(createdOrder.OrderDate.Value) : DateTimeOffset.Now,
                Items = kitchenItems,
                KitchenNote = order.OrderNotice!,
                KitchenType = kitchen.KitchenName ?? $"Kitchen_{kitchen.Id}",
                TableName = order.TableName,
                IsFollowUp = isFollowUp,
                IsCopy = isCopy,
                IsArabic = _commonProperties.Language == "ar"
            };

            var document = new KitchenReceiptDocument(receipt, kitchenItems);
            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var outputPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_{order.OrderType}_{receipt.KitchenType}_{DateTime.Now:yyyyMMddHHmmss}.pdf");

            try
            {
                document.GeneratePdf(outputPath);

                for (int i = 1; i <= receiptCount; i++)
                {
                    var printerName = targetPrinterName ?? (typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string);
                    if (!string.IsNullOrEmpty(printerName))
                    {
                        Log.Information("Printing separate kitchen ticket for {Kitchen} to {Printer}", kitchen.KitchenName, printerName);
                        await _printerServices.PrintPdfAsync(outputPath, printerName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error printing separate kitchen ticket for {KitchenName}", kitchen.KitchenName);
            }
        }
    }
    public async Task<bool> PrintDeliveryOrder(decimal paid = 0)
    {
        await BackupDeliveryOrderDtoDetails(paid);

        _commonProperties.OrderDto!.SkipPrintingOnServer = true;

        OrderDto? result;
        bool isFollowUp = _commonProperties.UpdateDeliveryOrder;

        if (_commonProperties.UpdateDeliveryOrder)
            result = await _orderSettingsService.UpdateOrderAsync(_commonProperties.OrderDto!);
        else
            result = await _orderSettingsService.CreateOrderAsync(_commonProperties.OrderDto!);

        if (result is null)
            return false;

        _commonProperties.CurrentOrderId = result.OrderId + 1;
        if (result.OrderDate.HasValue)
            _commonProperties.PosDate = DateOnly.FromDateTime(result.OrderDate.Value);

        try
        {
            decimal? parentDeliveryFees = null;
            if (isFollowUp && _commonProperties.OrderDto.ParentOrderId.HasValue)
                parentDeliveryFees = _commonProperties.OrderDto.DeliveryFees;

            // Ensure OrderSettings is available for printing
            if (_commonProperties.OrderSettings == null || !_commonProperties.OrderSettings.Any())
            {
                try
                {
                    var freshSettings = await _orderSettingsService.GetOrderSettingsAsync(Environment.MachineName);
                    if (freshSettings != null && freshSettings.Any())
                    {
                        _commonProperties.OrderSettings = freshSettings;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to refresh OrderSettings in DesktopPrintOrderService");
                }
            }
            _commonProperties.OrderDto.OrderSettings = _commonProperties.OrderSettings;

            await PrintDeliveryLocally(_commonProperties.OrderDto!, result, isFollowUp, parentDeliveryFees);

            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery")
                ?? _commonProperties.OrderDto.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");

            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(_commonProperties.OrderDto!, result, isReprint: false, isFollowUp: isFollowUp);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(_commonProperties.OrderDto!, result, isReprint: false, isFollowUp: isFollowUp);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print delivery order locally");
        }

        return true;
    }

    private async Task BackupDeliveryOrderDtoDetails(decimal paid = 0)
    {
        if (!_commonProperties.UpdateDeliveryOrder)
            _commonProperties.OrderDto!.OrderId = _commonProperties.CurrentOrderId;

        _commonProperties.OrderDto!.OrderType = "Delivery";
        _commonProperties.OrderDto.CashierName = _commonProperties.CurrentUser;
        _commonProperties.OrderDto.MachineName = Environment.MachineName;

        // Hospitality & Staff Meals
        _commonProperties.OrderDto.IsHospitality = _commonProperties.IsHospitalityMode;
        _commonProperties.OrderDto.HospitalityResponsibleName = _commonProperties.HospitalityResponsiblePerson;
        _commonProperties.OrderDto.HospitalityReason = _commonProperties.HospitalityReason;
        _commonProperties.OrderDto.IsStaffMeal = _commonProperties.CurrentStaffMeal != null;
        _commonProperties.OrderDto.StaffMealEmployeeId = _commonProperties.CurrentStaffMeal?.UserId;
        _commonProperties.OrderDto.StaffMealEmployeeName = _commonProperties.CurrentStaffMeal?.UserName;

        _commonProperties.OrderDto.CashierId = _commonProperties.CurrentUserId;
        _commonProperties.OrderDto.FooterMessage = _commonProperties.DeliverySettings?.OrderStatment ?? "";
        _commonProperties.OrderDto.PaymentMethod = _commonProperties.SelectedPaymentMethod;
        _commonProperties.OrderDto.Paid = paid > 0.00m ? paid : (_commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 4 ? _commonProperties._financeSettingsList[4].Value : 0);
        _commonProperties.OrderDto.SubTotal = _commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 0 ? _commonProperties._financeSettingsList[0].Value : 0;
        _commonProperties.OrderDto.Services = 0M;
        _commonProperties.OrderDto.Tax = _commonProperties._financeSettingsList?.Count > 2 ? _commonProperties._financeSettingsList[2].Value : 0M;
        _commonProperties.OrderDto.TotalOrderDiscount = _commonProperties.TotalDiscount;
        _commonProperties.OrderDto.TotalDiscount = _commonProperties.TotalDiscount;
        _commonProperties.OrderDto.DiscountPercentage = _commonProperties.OrderDiscount?.Percentage ?? 0;
        _commonProperties.OrderDto.DiscountReason = _commonProperties.OrderDiscount?.DiscountReason;
        _commonProperties.OrderDto.DiscountType = _commonProperties.OrderDiscount?.DiscountType;
        _commonProperties.OrderDto.DiscountByName = _commonProperties.CurrentUser;
        _commonProperties.OrderDto.DiscountTime = DateTime.Now;
        _commonProperties.OrderDto.GrandTotal = _commonProperties._financeSettingsList != null && _commonProperties._financeSettingsList.Count > 4 ? _commonProperties._financeSettingsList[4].Value : 0;
        _commonProperties.OrderDto.OrderDate = DateTime.Now;
        if (!_commonProperties.UpdateDeliveryOrder)
            _commonProperties.OrderDto.OrderState = "Pending";

        _commonProperties.OrderDto.OrderNotice = _commonProperties.OrderNote;
        _commonProperties.OrderDto.OrderDetails = _commonProperties.TableItems;

        _commonProperties.OrderDto.CustomerName = _commonProperties.CustomerDetails?.CustomerName;
        _commonProperties.OrderDto.Phone1 = _commonProperties.CustomerDetails?.FirstPhoneNumber;
        _commonProperties.OrderDto.Phone2 = _commonProperties.CustomerDetails?.SecondPhoneNumber;
        _commonProperties.OrderDto.StreetName = _commonProperties.CustomerDetails?.ClientAddress;
        _commonProperties.OrderDto.ZoneBonus = _commonProperties.CustomerDetails?.ZoneBonus ?? 0;
        _commonProperties.OrderDto.DeliveryFees = _commonProperties.CustomerDetails?.ZoneFees;
        _commonProperties.OrderDto.ZoneName = _commonProperties.CustomerDetails?.ZoneName;
        _commonProperties.OrderDto.AddressNotice = _commonProperties.CustomerDetails?.AddressNote;
        _commonProperties.OrderDto.HomeNum = _commonProperties.CustomerDetails?.HomeNumber;
        _commonProperties.OrderDto.FloorNum = _commonProperties.CustomerDetails?.FloorNumber;
        _commonProperties.OrderDto.ApartmentNum = _commonProperties.CustomerDetails?.FlatNumber;

        _commonProperties.OrderDto.TakerID = _commonProperties.CurrentUserId;
        _commonProperties.OrderDto.TakerName = _commonProperties.CurrentUser;
        _commonProperties.OrderDto.CustomerID = _commonProperties.CustomerDetails?.Id;
        _commonProperties.OrderDto.TitleName = _commonProperties.CustomerDetails?.ClientTitle;
        _commonProperties.OrderDto.ZoneID = _commonProperties.CustomerDetails?.ZoneID;

        _commonProperties.OrderDto.OrderSettings = _commonProperties.OrderSettings;
        _commonProperties.OrderDto.MachineName = Environment.MachineName;

        // Determine if this is a Call Center setup
        bool isCallCenter = _commonProperties.IsFeatureEnabled("IsCallCenter");

        // Single call to fetch branches for all resolution logic
        var allBranches = await _branchService.GetBranches();

        if (isCallCenter && _commonProperties.CustomerDetails?.BranchId > 0)
        {
            // Call Center: Route to the zone's destination branch
            _commonProperties.OrderDto.BranchId = _commonProperties.CustomerDetails.BranchId;
            _commonProperties.OrderDto.BranchName = _commonProperties.CustomerDetails.BranchName;

            // Use BranchApiUrl from CustomerDetails (set when zone/branch was selected)
            // If empty → branch has no API URL → falls back (API will reject dispatch, order stays at CC)
            var branchApiUrl = _commonProperties.CustomerDetails.BranchApiUrl;

            if (string.IsNullOrWhiteSpace(branchApiUrl))
            {
                // No remote API → find by ID and try ApiUrl again from DB record
                var targetBranch = allBranches.FirstOrDefault(b => b.Id == _commonProperties.CustomerDetails.BranchId);
                branchApiUrl = targetBranch?.ApiUrl;
            }

            // If still empty → no URL configured for this branch
            // API will then print locally at Call Center (fallback handled in OrderController)
            _commonProperties.OrderDto.DeliveryBranchUrl = branchApiUrl;
        }
        else if (!isCallCenter)
        {
            // Regular branch delivery (not call center)
            if (_commonProperties.BranchDetails == null || string.IsNullOrWhiteSpace(_commonProperties.BranchDetails.Name))
            {
                var firstBranch = allBranches.FirstOrDefault();
                _commonProperties.OrderDto.BranchId = firstBranch?.Id ?? 1;
                _commonProperties.OrderDto.BranchName = firstBranch?.Name ?? _commonProperties.StoreName;
            }
            else
            {
                _commonProperties.OrderDto.BranchId = _commonProperties.BranchDetails.Id;
                _commonProperties.OrderDto.BranchName = _commonProperties.BranchDetails.Name;
            }
            // No DeliveryBranchUrl for local branch delivery
        }
    }


    private async Task PrintDeliveryLocally(OrderDto orderDto,
        OrderDto createdOrder, bool isFollowUp = false, decimal? parentDeliveryFees = null, bool isCopy = false, string? printerName = null, bool neverCopy = false)
    {
        try
        {
            var branch = await _branchService.GetBranches();
            var currentBranch = branch.ElementAtOrDefault(0);
            var firstBranch = branch.ElementAtOrDefault(0);
            var receiptName = !string.IsNullOrWhiteSpace(orderDto.BranchName) ? orderDto.BranchName : firstBranch?.Name;
            if (string.IsNullOrWhiteSpace(receiptName) || receiptName.Equals("Store", StringComparison.OrdinalIgnoreCase))
                receiptName = firstBranch?.Name ?? "Store";

            var receipt = new DeliveryReceipt()
            {
                Id = createdOrder.ParentOrderId ?? createdOrder.OrderId,
                ParentOrderId = createdOrder.ParentOrderId,
                IsFollowUp = isFollowUp,

                StoreName = receiptName,
                CashierName = orderDto.CashierName ?? "Cashier",
                ReceiptType = orderDto.OrderType ?? "Delivery",
                DateCreated = createdOrder.OrderDate ?? DateTime.Now,
                PaymentMethod = orderDto.PaymentMethod.ToString() ?? "Cash",
                FooterMessage = orderDto.FooterMessage ?? "",
                LogoPath = LogoPath,
                LogoWidth = currentBranch?.LogoWidth ?? 150,
                CustomerName = orderDto.CustomerName,
                CustomerFirstPhone = orderDto.Phone1,
                CustomerSecondPhone = orderDto.Phone2,
                CustomerAddress = orderDto.StreetName,
                DeliveryName = orderDto.DriverName,

                DeliveryFees = (orderDto.DeliveryFees ?? 0) != 0 ? (orderDto.DeliveryFees ?? 0)
                             : (createdOrder.DeliveryFees ?? 0) != 0 ? (createdOrder.DeliveryFees ?? 0)
                             : (parentDeliveryFees ?? 0),

                ZoneName = orderDto.ZoneName,
                AddressNote = orderDto.AddressNotice,
                Building = orderDto.HomeNum,
                HomeNumber = orderDto.HomeNum,
                FloorNumber = orderDto.FloorNum,
                FlatNumber = orderDto.ApartmentNum,
                TotalOrder = createdOrder.GrandTotal ?? orderDto.GrandTotal,
                TotalAmount = createdOrder.SubTotal ?? orderDto.SubTotal
                              ?? orderDto.OrderDetails?.Sum(i => i.TotalAmount ?? (i.Price * i.Quantity) ?? 0),
                IsVoid = orderDto.OrderState == "Voided",
                IsHospitality = orderDto.IsHospitality ?? false,
                HospitalityResponsibleName = orderDto.HospitalityResponsibleName,
                HospitalityReason = orderDto.HospitalityReason,
                IsStaffMeal = orderDto.IsStaffMeal ?? false,
                StaffMealEmployeeName = orderDto.StaffMealEmployeeName
            };

            var orderSettings = orderDto.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery")
                ?? _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");
            int currentPrintCount = 0;
            try
            {
                if (!isCopy && !neverCopy)
                {
                    currentPrintCount = await _orderSettingsService.IncrementPrintCountAsync(createdOrder.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error incrementing print count for Delivery order {Id}", createdOrder.Id);
            }

            if (orderSettings != null && orderSettings.CustomerReceiptCount > 0)
            {
                var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
                var customerKitchen = kitchens?.FirstOrDefault(k => k.KitchenName != null && k.KitchenName.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                    ?? kitchens?.ElementAtOrDefault(0);

                if (customerKitchen != null)
                {
                    var printers = customerKitchen.KitchenPrinters?.FirstOrDefault(p => p.DeviceName == Environment.MachineName)
                        ?? customerKitchen.KitchenPrinters?.FirstOrDefault();
                    if (printers != null)
                    {

                        string? originalPath = null;
                        string? copyPath = null;
                        var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                        Directory.CreateDirectory(reportsFolder);

                        var receiptItems = createdOrder.OrderDetails?.Any() == true 
                            ? createdOrder.OrderDetails 
                            : (orderDto.OrderDetails ?? new List<TableItem>());

                        for (int i = 1; i <= orderSettings.CustomerReceiptCount; i++)
                        {
                            var targetPrinter = printerName ?? (typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string);
                            if (string.IsNullOrEmpty(targetPrinter)) continue;

                            bool isActuallyCopy = !isFollowUp && !neverCopy && (isCopy || (currentPrintCount > 1) || (i > 1));

                            string pathToPrint;
                            if (isActuallyCopy)
                            {
                                if (copyPath == null)
                                {
                                    receipt.IsCopy = true;
                                    var document = new DeliveryReceiptDocument(receipt, receiptItems);
                                    copyPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_Delivery_Copy_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                    document.GeneratePdf(copyPath);
                                }
                                pathToPrint = copyPath;
                            }
                            else
                            {
                                if (originalPath == null)
                                {
                                    receipt.IsCopy = false;
                                    var document = new DeliveryReceiptDocument(receipt, receiptItems);
                                    originalPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_Delivery_Original_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                    document.GeneratePdf(originalPath);
                                }
                                pathToPrint = originalPath;
                            }

                            try
                            {
                                await _printerServices.PrintPdfAsync(pathToPrint, targetPrinter);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error printing Delivery to printer {PrinterName}", printerName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print delivery locally for order {OrderId}", createdOrder.OrderId);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _recentlyPrintedOrders = new();

    public async Task PrintReceivedOrderAsync(OrderDto order)
    {
        try
        {
            var orderKey = $"{order.Id}_{order.OrderId}";
            if (_recentlyPrintedOrders.TryGetValue(orderKey, out var lastPrintTime) && (DateTime.UtcNow - lastPrintTime).TotalSeconds < 15)
            {
                Log.Warning("[Print Guard] Order {Key} was already printed {Seconds:F1}s ago. Skipping duplicate print.", orderKey, (DateTime.UtcNow - lastPrintTime).TotalSeconds);
                return;
            }
            _recentlyPrintedOrders[orderKey] = DateTime.UtcNow;

            // Clean up keys older than 1 minute to keep dictionary compact
            foreach (var kvp in _recentlyPrintedOrders)
            {
                if ((DateTime.UtcNow - kvp.Value).TotalMinutes > 1)
                    _recentlyPrintedOrders.TryRemove(kvp.Key, out _);
            }

            // Always fetch fresh settings for this machine to respect any BackOffice updates at branch
            try
            {
                var freshSettings = await _orderSettingsService.GetOrderSettingsAsync(Environment.MachineName);
                if (freshSettings != null && freshSettings.Any())
                {
                    _commonProperties.OrderSettings = freshSettings;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PrintReceivedOrderAsync] Failed to fetch fresh order settings, falling back to cached settings.");
            }

            order.OrderSettings = _commonProperties.OrderSettings;

            await PrintDeliveryLocally(order, order);

            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");

            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(order, order, isReprint: true);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(order, order, isReprint: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print received order locally");
        }
    }

    public async Task PrintFollowUpOrderAsync(OrderDto order, List<TableItem>? addedItems = null)
    {
        try
        {
            var itemsCount = addedItems?.Count ?? order.OrderDetails?.Count ?? 0;
            var orderKey = $"followup_{order.Id}_{order.OrderId}_{itemsCount}";
            if (_recentlyPrintedOrders.TryGetValue(orderKey, out var lastPrintTime) && (DateTime.UtcNow - lastPrintTime).TotalSeconds < 10)
            {
                Log.Warning("[Print Guard] Follow-up order {Key} was already printed {Seconds:F1}s ago. Skipping duplicate print.", orderKey, (DateTime.UtcNow - lastPrintTime).TotalSeconds);
                return;
            }
            _recentlyPrintedOrders[orderKey] = DateTime.UtcNow;

            // Always fetch fresh settings for this machine to respect any BackOffice updates at branch
            try
            {
                var freshSettings = await _orderSettingsService.GetOrderSettingsAsync(Environment.MachineName);
                if (freshSettings != null && freshSettings.Any())
                {
                    _commonProperties.OrderSettings = freshSettings;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PrintFollowUpOrderAsync] Failed to fetch fresh order settings, falling back to cached settings.");
            }

            order.OrderSettings = _commonProperties.OrderSettings;

            // 1. Print Customer Delivery receipt marked as Follow-Up
            await PrintDeliveryLocally(order, order, isFollowUp: true);

            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");

            if (orderSettings != null)
            {
                // For kitchen and backup: if specific added items were provided, print only those! Otherwise print all items.
                var itemsToPrint = (addedItems != null && addedItems.Any()) ? addedItems : order.OrderDetails;

                if (itemsToPrint != null && itemsToPrint.Any())
                {
                    await EnrichItemsWithKitchenTypesAsync(itemsToPrint);

                    var itemsDto = new OrderDto
                    {
                        Id = order.Id,
                        OrderId = order.OrderId,
                        OrderType = order.OrderType ?? "Delivery",
                        CashierName = order.CashierName,
                        TableName = order.TableName,
                        OrderNotice = order.OrderNotice,
                        OrderDate = order.OrderDate,
                        OrderSettings = order.OrderSettings,
                        OrderDetails = itemsToPrint
                    };

                    if (orderSettings.FullKitchenReceiptCount > 0)
                        await PrintBackupReceiptsLocally(itemsDto, itemsDto, isReprint: true, isFollowUp: true);

                    if (orderSettings.SeparateReceiptCount > 0)
                        await PrintKitchenReceiptsLocally(itemsDto, itemsDto, isReprint: true, isFollowUp: true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print follow-up order locally");
        }
    }
    public async Task PrintDispatchOrderAsync(OrderDto order)
    {
        try
        {
            order.OrderSettings = _commonProperties.OrderSettings;

            await PrintDispatchLocally(order, order);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to Print Dispatch Order Locally");
        }
    }

    private async Task PrintDispatchLocally(OrderDto orderDto,
        OrderDto createdOrder,
        bool isFollowUp = false,
        decimal? parentDeliveryFees = null)
    {
        var branch = await _branchService.GetBranches();
        var currentBranch = branch.FirstOrDefault(b => b.Id == orderDto.BranchId);

        var receipt = new DeliveryReceipt()
        {
            Id = createdOrder.ParentOrderId ?? createdOrder.OrderId,
            ParentOrderId = createdOrder.ParentOrderId,
            IsFollowUp = isFollowUp,

            StoreName = orderDto.BranchName ?? "Store",
            CashierName = orderDto.CashierName ?? "Cashier",
            ReceiptType = orderDto.OrderType ?? "Delivery",
            DateCreated = createdOrder.OrderDate ?? DateTime.Now,
            PaymentMethod = orderDto.PaymentMethod.ToString() ?? "Cash",
            FooterMessage = orderDto.FooterMessage ?? "",
            LogoPath = LogoPath,
            LogoWidth = currentBranch?.LogoWidth ?? 150,
            CustomerName = orderDto.CustomerName,
            CustomerFirstPhone = orderDto.Phone1,
            CustomerSecondPhone = orderDto.Phone2,
            CustomerAddress = orderDto.StreetName,
            DeliveryName = orderDto.DriverName,

            DeliveryFees = (orderDto.DeliveryFees ?? 0) != 0 ? (orderDto.DeliveryFees ?? 0)
                         : (createdOrder.DeliveryFees ?? 0) != 0 ? (createdOrder.DeliveryFees ?? 0)
                         : (parentDeliveryFees ?? 0),

            ZoneName = orderDto.ZoneName,
            AddressNote = orderDto.AddressNotice,
            Building = orderDto.HomeNum,
            HomeNumber = orderDto.HomeNum,
            FloorNumber = orderDto.FloorNum,
            FlatNumber = orderDto.ApartmentNum,
            TotalAmount = orderDto.SubTotal,
            TotalOrder = orderDto.GrandTotal
        };

        var orderSettings = 
            orderDto.OrderSettings?.FirstOrDefault(o => o.OrderType == "Delivery");
        int currentPrintCount = 0;
        try
        {
            currentPrintCount = await _orderSettingsService.IncrementPrintCountAsync(createdOrder.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error incrementing print count for Dispatch order {Id}", createdOrder.Id);
        }

        if (orderSettings != null && orderSettings.CustomerReceiptCount > 0)
        {
            var kitchens = await _printerServices.GetKitchenTypesAsync();
            var customerKitchen = kitchens?.ElementAtOrDefault(0);

            if (customerKitchen != null)
            {
                var printers = customerKitchen.KitchenPrinters?.FirstOrDefault();
                if (printers != null)
                {

                    string? originalPath = null;
                    string? copyPath = null;
                    var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                    Directory.CreateDirectory(reportsFolder);

                    for (int i = 1; i <= orderSettings.CustomerReceiptCount; i++)
                    {
                        var printerName = typeof(KitchenPrinters).GetProperty($"Copy{i}")?.GetValue(printers) as string;
                        if (string.IsNullOrEmpty(printerName)) continue;

                        bool isActuallyCopy = (currentPrintCount > 1) || (i > 1);

                        string pathToPrint;
                        if (isActuallyCopy)
                        {
                            if (copyPath == null)
                            {
                                receipt.IsCopy = false;
                                var document = new DeliveryReceiptDocument(receipt, orderDto.OrderDetails ?? new List<TableItem>());
                                copyPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_Dispatch_Copy_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                document.GeneratePdf(copyPath);
                            }
                            pathToPrint = copyPath;
                        }
                        else
                        {
                            if (originalPath == null)
                            {
                                receipt.IsCopy = false;
                                var document = new DeliveryReceiptDocument(receipt, orderDto.OrderDetails ?? new List<TableItem>());
                                originalPath = Path.Combine(reportsFolder, $"Order_{createdOrder.OrderId}_Dispatch_Original_{DateTime.Now:yyyyMMddHHmmss}_{i}.pdf");
                                document.GeneratePdf(originalPath);
                            }
                            pathToPrint = originalPath;
                        }

                        try
                        {
                            await _printerServices.PrintPdfAsync(pathToPrint, printerName);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error printing Dispatch to printer {PrinterName}", printerName);
                        }
                    }
                }
            }
        }
    }
    public async Task PrintVoidReceiptAsync(OrderDto order, List<TableItem> voidedItems)
    {
        if (voidedItems == null || !voidedItems.Any()) return;

        var voidKey = $"{order.OrderId}_{order.Id}";
        if (_lastVoidPrints.TryGetValue(voidKey, out var lastTime) && (DateTime.Now - lastTime).TotalSeconds < 5)
        {
            Log.Information("Suppressing duplicate void print for order {OrderId}", order.OrderId);
            return;
        }
        _lastVoidPrints[voidKey] = DateTime.Now;

        // 1. Kitchen and Backup VOID tickets
        await PrintVoidReceiptInternal(order.OrderId, order.CashierName, order.OrderType ?? "Order", order.OrderDate, order.OrderNotice, order.TableName, voidedItems);

        // 2. Customer VOID / Updated receipt (Delivery / TakeAway)
        try
        {
            var previousState = order.OrderState;
            bool isFullVoid = string.Equals(order.OrderState, "Voided", StringComparison.OrdinalIgnoreCase)
                || (order.OrderDetails != null && order.OrderDetails.All(i => i.Quantity == 0 || i.IsVoided == true));

            if (isFullVoid)
            {
                order.OrderState = "Voided";
            }
            try
            {
                if (string.Equals(order.OrderType, "Delivery", StringComparison.OrdinalIgnoreCase))
                {
                    await PrintDeliveryLocally(order, order, neverCopy: true);
                }
                else if (string.Equals(order.OrderType, "TakeAway", StringComparison.OrdinalIgnoreCase))
                {
                    await PrintTakeAwayLocally(order, order, isCopy: false);
                }
            }
            finally
            {
                order.OrderState = previousState;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing customer void receipt for order {OrderId}", order.OrderId);
        }
    }

    public async Task PrintDineInVoidReceiptAsync(DineInOrderDto order, List<TableItem> voidedItems)
    {
        if (voidedItems == null || !voidedItems.Any()) return;

        // Dedup: same key as PrintVoidReceiptAsync - suppress double-printing when
        // VoidOrderDialog AND SignalR notification both trigger void printing
        var voidKey = $"{order.OrderId}_{order.Id}";
        if (_lastVoidPrints.TryGetValue(voidKey, out var lastTime) && (DateTime.Now - lastTime).TotalSeconds < 5)
        {
            Log.Information("Suppressing duplicate DineIn void print for order {OrderId}", order.OrderId);
            return;
        }
        _lastVoidPrints[voidKey] = DateTime.Now;

        // Use OrderId (display number) not Id (database key)
        await PrintVoidReceiptInternal(order.OrderId, order.CashierName, "DineIn", order.OrderDateTime, order.OrderNotice, order.TableName, voidedItems);
    }

    private async Task EnrichItemsWithKitchenTypesAsync(IEnumerable<TableItem>? items)
    {
        if (items == null || _itemService == null) return;
        foreach (var item in items)
        {
            if (item.Id <= 0) continue;

            try
            {
                if (_itemKitchenCache.TryGetValue(item.Id, out var cached))
                {
                    item.ItemKitchenTypeId = cached.kitchenTypeId;
                    item.CategoryKitchenTypeId = cached.catKitchenTypeId;
                    if (cached.backupItem.HasValue) item.PrintInBackupReceiptFromItem = cached.backupItem;
                    if (cached.backupCat.HasValue) item.PrintInBackupReceiptFromCategory = cached.backupCat;
                }
                else
                {
                    var itemResp = await _itemService.GetItemByIdAsync(item.Id);
                    if (itemResp?.Success == true && itemResp.Data != null)
                    {
                        item.ItemKitchenTypeId = itemResp.Data.KitchenTypeId;
                        item.CategoryKitchenTypeId = itemResp.Data.CategoryKitchenTypeId;
                        item.PrintInBackupReceiptFromItem = itemResp.Data.PrintInBackupReceipt;
                        item.PrintInBackupReceiptFromCategory = itemResp.Data.PrintInBackupReceiptFromCategory;

                        _itemKitchenCache[item.Id] = (itemResp.Data.KitchenTypeId, itemResp.Data.CategoryKitchenTypeId, itemResp.Data.PrintInBackupReceipt, itemResp.Data.PrintInBackupReceiptFromCategory);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DesktopPrintOrderService] Failed to enrich kitchen type for item {ItemId}", item.Id);
            }
        }
    }

    private async Task PrintVoidReceiptInternal(int orderId, string? cashier, string orderType, DateTime? date, string? notice, string? tableName, List<TableItem> items)
    {
        if (_commonProperties.IsFeatureEnabled("IsCallCenter")) return;
        await EnrichItemsWithKitchenTypesAsync(items);
        // Similar to PrintKitchenReceiptsLocally but forced to VOID type
        var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
        if (kitchens == null || !kitchens.Any()) return;

        var groups = items
            .Where(item => item.ItemKitchenTypeId.HasValue || item.CategoryKitchenTypeId.HasValue)
            .GroupBy(item => item.ItemKitchenTypeId ?? item.CategoryKitchenTypeId!.Value)
            .ToList();

        foreach (var group in groups)
        {
            var kitchenId = group.Key;
            var kitchenItems = group.ToList();
            var kitchen = kitchens.FirstOrDefault(k => k.Id == kitchenId);

            if (kitchen == null) continue;
            // Do not print void receipt to the Customer kitchen (index 0) — only production/backup kitchens get void tickets
            // Skip Customer AND Backup kitchens in the group loop — Backup has its own dedicated block below
            if (string.Equals(kitchen.KitchenName, "Customer", StringComparison.OrdinalIgnoreCase) || string.Equals(kitchen.KitchenName, "Backup", StringComparison.OrdinalIgnoreCase) || string.Equals(kitchen.KitchenName, "Packup", StringComparison.OrdinalIgnoreCase)) continue;
            var printers = kitchen.KitchenPrinters?.FirstOrDefault();
            if (printers == null) continue;

            var receipt = new KitchenReceipt()
            {
                Id = orderId,
                CashierName = cashier ?? "Cashier",
                OrderType = orderType,
                DateCreated = date.HasValue ? new DateTimeOffset(date.Value) : DateTimeOffset.Now,
                Items = kitchenItems,
                KitchenNote = notice ?? "",
                KitchenType = $"{kitchen.KitchenName ?? "Kitchen"} - ملغي",
                TableName = tableName,
                IsVoid = true,
                IsArabic = _commonProperties.Language == "ar"
            };

            // Add a special flag or handle in PDF generation for RED VOID text if possible, 
            // but for now relying on the KitchenType name containing "VOID".

            var document = new KitchenReceiptDocument(receipt, kitchenItems);
            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var outputPath = Path.Combine(reportsFolder, $"Order_{orderId}_VOID_{receipt.KitchenType}_{DateTime.Now:yyyyMMddHHmmss}.pdf");

            try
            {
                document.GeneratePdf(outputPath);

                // Print 1 copy to the main printer
                var printerName = printers.Copy1;
                if (!string.IsNullOrEmpty(printerName))
                {
                    Log.Information("Printing VOID ticket for {Kitchen} to {Printer}", kitchen.KitchenName, printerName);
                    await _printerServices.PrintPdfAsync(outputPath, printerName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error printing VOID ticket for {KitchenName}", kitchen.KitchenName);
            }
        }

        // ─── Also print VOID on the Backup/Packup printer ───
        try
        {
            var backupItems = items
                .Where(i => i.PrintInBackupReceiptFromItem ?? i.PrintInBackupReceiptFromCategory ?? true)
                .ToList();

            if (backupItems.Any())
            {
                var backupReceipt = new KitchenReceipt()
                {
                    Id = orderId,
                    CashierName = cashier ?? "Cashier",
                    OrderType = orderType,
                    DateCreated = date.HasValue ? new DateTimeOffset(date.Value) : DateTimeOffset.Now,
                    Items = backupItems,
                    KitchenNote = notice ?? "",
                    KitchenType = "Packup",
                    TableName = tableName,
                    IsVoid = true,
                    IsArabic = _commonProperties.Language == "ar"
                };

                var backupDocument = new KitchenReceiptDocument(backupReceipt, backupItems);
                var reportsFolder2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                Directory.CreateDirectory(reportsFolder2);
                var backupOutputPath = Path.Combine(reportsFolder2, $"Order_{orderId}_VOID_Backup_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                backupDocument.GeneratePdf(backupOutputPath);

                var backupKitchen = kitchens.FirstOrDefault(k => k.KitchenName != null &&
                    (k.KitchenName.Equals("Backup", StringComparison.OrdinalIgnoreCase) ||
                     k.KitchenName.Equals("Packup", StringComparison.OrdinalIgnoreCase)))
                    ?? kitchens.ElementAtOrDefault(1)
                    ?? kitchens.FirstOrDefault();

                if (backupKitchen != null)
                {
                    var backupPrinters = backupKitchen.KitchenPrinters?.FirstOrDefault(p => p.DeviceName == Environment.MachineName)
                        ?? backupKitchen.KitchenPrinters?.FirstOrDefault();

                    if (backupPrinters != null)
                    {
                        var settingsOrderType = (orderType == "Staff" || orderType == "Hospitality") ? "TakeAway" : orderType;
                        var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => string.Equals(o.OrderType, settingsOrderType, StringComparison.OrdinalIgnoreCase));
                        var receiptCount = orderSettings?.FullKitchenReceiptCount ?? 1;

                        for (int idx = 1; idx <= receiptCount; idx++)
                        {
                            var backupPrinterName = backupPrinters.GetType().GetProperty($"Copy{idx}")?.GetValue(backupPrinters) as string;
                            if (!string.IsNullOrEmpty(backupPrinterName))
                            {
                                Log.Information("Printing VOID backup receipt to {Printer}", backupPrinterName);
                                await _printerServices.PrintPdfAsync(backupOutputPath, backupPrinterName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing VOID backup receipt for order {OrderId}", orderId);
        }
    }

    public async Task PrintSalesSummaryAsync(SalesSummaryDto summary, 
        List<SalesItemSummaryDto> items,
        string? printerName = null, 
        bool useA4 = false, 
        bool isArabic = true)
    {
        try
        {
            var branch = await _branchService.GetBranches();
            var currentBranch = branch.FirstOrDefault(b => b.Id == _commonProperties.BranchDetails?.Id);

            var format = useA4 ? ReportPageFormat.A4 : ReportPageFormat.Cashier;
            var document = new SalesSummaryDocument(summary, items, currentBranch?.Name ?? "Store", LogoPath, format, isArabic);

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var formatSuffix = useA4 ? "A4" : "Cashier";
            var langSuffix = isArabic ? "AR" : "EN";
            var outputPath = Path.Combine(reportsFolder, $"SalesSummary_{summary.PosDate:yyyyMMdd}_{formatSuffix}_{langSuffix}_{DateTime.Now:HHmmss}.pdf");

            document.GeneratePdf(outputPath);

            if (string.IsNullOrWhiteSpace(printerName))
            {
                // Try to get system default printer
                printerName = new PrinterSettings().PrinterName;
                Log.Information("No printer specified. Using system default: {Printer}", printerName);
            }

            if (string.IsNullOrWhiteSpace(printerName))
            {
                // Fallback to kitchen settings if still no printer
                var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
                var mainKitchen = kitchens?.ElementAtOrDefault(0);
                printerName = mainKitchen?.KitchenPrinters?.FirstOrDefault()?.Copy1;
                Log.Information("System default null. Trying kitchen fallback: {Printer}", printerName);
            }

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                Log.Information("Final printer choice for Sales Summary: {Printer}", printerName);
                bool success = await _printerServices.PrintPdfAsync(outputPath, printerName);
                if (!success)
                {
                    Log.Warning("PrintPdfAsync returned false for {Printer}", printerName);
                    throw new Exception($"Failed to print to {printerName}. Check if printer is connected.");
                }
            }
            else
            {
                Log.Warning("Critical: No printer found anywhere to print Sales Summary.");
                throw new Exception("No printer found on this system.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print sales summary");
        }
    }

    public async Task PrintEndDayReportAsync(SalesSummaryDto summary, List<SalesItemSummaryDto> items, string? printerName = null, bool useA4 = false, bool isArabic = true)
    {
        try
        {
            var branches = await _branchService.GetBranches();
            var currentBranch = branches.FirstOrDefault();

            var format = useA4 ? ReportPageFormat.A4 : ReportPageFormat.Cashier;
            var document = new EndDayDocument(summary, items, currentBranch?.Name ?? "Store", LogoPath, format, isArabic, currentBranch?.Currency ?? "EGP");

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var formatSuffix = useA4 ? "A4" : "Cashier";
            var langSuffix = isArabic ? "AR" : "EN";
            var outputPath = Path.Combine(reportsFolder, $"EndDay_{summary.PosDate:yyyyMMdd}_{formatSuffix}_{langSuffix}_{DateTime.Now:HHmmss}.pdf");

            document.GeneratePdf(outputPath);

            if (string.IsNullOrWhiteSpace(printerName))
            {
                printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                Log.Information("No printer specified for EndDay. Using system default: {Printer}", printerName);
            }

            if (string.IsNullOrWhiteSpace(printerName))
            {
                var kitchens = await _printerServices.GetKitchenTypesAsync(Environment.MachineName);
                var mainKitchen = kitchens?.ElementAtOrDefault(0);
                printerName = mainKitchen?.KitchenPrinters?.FirstOrDefault()?.Copy1;
                Log.Information("System default null for EndDay. Trying kitchen fallback: {Printer}", printerName);
            }

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                Log.Information("Final printer choice for End Day Report: {Printer}", printerName);
                bool success = await _printerServices.PrintPdfAsync(outputPath, printerName);
                if (!success)
                {
                    Log.Warning("PrintPdfAsync returned false for EndDay on {Printer}", printerName);
                    throw new Exception($"Failed to print to {printerName}. Check if printer is connected.");
                }
            }
            else
            {
                Log.Warning("Critical: No printer found anywhere to print End Day Report.");
                throw new Exception("No printer found on this system.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print end day report");
        }
    }

    public async Task PrintDriverSettlementAsync(DriverSettlementDto settlement, DateTime posDate, string? printerName = null)
    {
        try
        {
            var branches = await _branchService.GetBranches();
            var currentBranch = branches.FirstOrDefault(b => b.Id == _commonProperties.BranchDetails?.Id);
            var branchName = currentBranch?.Name ?? branches.FirstOrDefault()?.Name ?? "Store";

            var document = new DriverSettlementDocument(
                settlement, 
                posDate, 
                branchName, 
                LogoPath, 
                ReportPageFormat.Cashier, 
                true);

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var outputPath = Path.Combine(reportsFolder, $"DriverSettlement_{settlement.DriverId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            document.GeneratePdf(outputPath);

            // PRINTER SELECTION LOGIC
            // 1. If explicit printerName provided
            if (string.IsNullOrWhiteSpace(printerName))
            {
                // 2. Try the stored cashier printer
                printerName = await _localStorage.GetItemAsStringAsync("cashier_printer_name");
                
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    // 3. Fallback to system default
                    printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                    Log.Information("No printer provided or stored. Using system default: {Printer}", printerName);
                }
            }

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                Log.Information("Printing Driver Settlement for {Driver} to {Printer}", settlement.DriverName, printerName);
                await _printerServices.PrintPdfAsync(outputPath, printerName);
            }
            else
            {
                Log.Warning("Critical: No printer found to print Driver Settlement.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print driver settlement for {Driver}", settlement.DriverName);
        }
    }

    public async Task PrintAllDriversSettlementAsync(List<DriverSettlementDto> settlements, DateTime posDate, string? printerName = null)
    {
        try
        {
            var branches = await _branchService.GetBranches();
            var currentBranch = branches.FirstOrDefault(b => b.Id == _commonProperties.BranchDetails?.Id);
            var branchName = currentBranch?.Name ?? branches.FirstOrDefault()?.Name ?? "Store";

            var document = new DriverAllSettlementDocument(
                settlements,
                posDate,
                branchName,
                LogoPath,
                ReportPageFormat.Cashier,
                isArabic: true,
                fromDate: posDate,
                toDate: posDate);

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var outputPath = Path.Combine(reportsFolder, $"AllDriversSettlement_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            document.GeneratePdf(outputPath);

            // Printer selection
            if (string.IsNullOrWhiteSpace(printerName))
            {
                printerName = await _localStorage.GetItemAsStringAsync("cashier_printer_name");

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                    Log.Information("No printer provided. Using system default: {Printer}", printerName);
                }
            }

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                Log.Information("Printing All Drivers Settlement ({Count} drivers) to {Printer}", settlements.Count, printerName);
                await _printerServices.PrintPdfAsync(outputPath, printerName);
            }
            else
            {
                Log.Warning("Critical: No printer found to print All Drivers Settlement.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print all drivers settlement");
        }
    }


    public async Task PrintStaffPerformanceAsync(SalesSummaryDto summary, string? printerName = null, bool useA4 = false, bool isArabic = true, bool showOrders = false, string? specificStaffId = null)
    {
        try
        {
            var branches = await _branchService.GetBranches();
            var currentBranch = branches.FirstOrDefault();

            var document = new StaffPerformanceDocument(summary, currentBranch?.Name ?? "Store", !useA4, isArabic, showOrders, specificStaffId);

            var reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsFolder);
            var formatSuffix = useA4 ? "A4" : "Cashier";
            var langSuffix = isArabic ? "AR" : "EN";
            var outputPath = Path.Combine(reportsFolder, $"StaffPerformance_{summary.PosDate:yyyyMMdd}_{formatSuffix}_{langSuffix}_{DateTime.Now:HHmmss}.pdf");

            document.GeneratePdf(outputPath);

            if (string.IsNullOrEmpty(printerName))
            {
                printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
            }

            if (!string.IsNullOrEmpty(printerName))
            {
                Log.Information("Printing Staff Performance ({Format}) to: {Printer}", formatSuffix, printerName);
                await _printerServices.PrintPdfAsync(outputPath, printerName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print staff performance report");
        }
    }

    public async Task<bool> PrintHospitalityOrder(string reason, List<TableItem> items)
    {
        var orderDto = CreateSpecialOrderDto("Hospitality", _commonProperties.HospitalityResponsiblePerson ?? "Hospitality", reason, "Hospitality", items);
        return await ProcessSpecialOrder(orderDto);
    }

    private OrderDto CreateSpecialOrderDto(string type, string name, string reason, string phone, List<TableItem> items)
    {
        var orderDto = new OrderDto
        {
            OrderId = _commonProperties.CurrentOrderId,
            OrderType = type,
            CashierName = _commonProperties.CurrentUser,
            CashierId = _commonProperties.CurrentUserId,
            BranchId = _commonProperties.BranchDetails?.Id ?? 1,
            BranchName = _commonProperties.BranchDetails?.Name ?? "Store",
            OrderDate = DateTime.Now,
            OrderState = "Completed",
            OrderNotice = _commonProperties.OrderNote,
            OrderDetails = items,
            CustomerName = name,
            CustomerPhone = phone,
            TakeAwayCustomerName = name,
            TakeawayCustomerPhone = phone,
            MachineName = Environment.MachineName,
            OrderSettings = _commonProperties.OrderSettings,
            IsHospitality = type == "Hospitality",
            HospitalityResponsibleName = type == "Hospitality" ? name : null,
            HospitalityReason = type == "Hospitality" ? reason : null,
            IsStaffMeal = type == "Staff",
            StaffMealEmployeeName = type == "Staff" ? name : null,
            // ZERO VALUES as requested: "لا يسمع فلوس"
            SubTotal = 0,
            GrandTotal = 0,
            Paid = 0,
            Remaining = 0,
            Tax = 0,
            Services = 0,
            TotalOrderDiscount = 0,
            PaymentMethod = PaymentMethod.Cash
        };

        // Also zero out each item's price just in case
        foreach (var item in items)
        {
            item.Price = 0;
            item.Total = 0;
            item.TotalAmount = 0;
            item.LineComment = type == "Staff" ? "Staff Meal" : "ضـيـافـة";
            item.IsStaffMeal = type == "Staff";
            item.StaffName = type == "Staff" ? name : null;
            item.IsHospitality = type == "Hospitality";
            item.HospitalityResponsibleName = type == "Hospitality" ? name : null;
        }

        return orderDto;
    }

    private async Task<bool> ProcessSpecialOrder(OrderDto orderDto)
    {
        orderDto.SkipPrintingOnServer = true;
        var result = await _orderSettingsService.CreateOrderAsync(orderDto);

        if (result == null) return false;

        _commonProperties.CurrentOrderId = result.OrderId + 1;
        if (result.OrderDate.HasValue)
            _commonProperties.PosDate = DateOnly.FromDateTime(result.OrderDate.Value);

        try
        {
            // For printing, we can reuse TakeAway local printing logic
            await PrintTakeAwayLocally(orderDto, result);

            // Print to kitchen
            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(orderDto, result, isReprint: true);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(orderDto, result, isReprint: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print {Type} order locally", orderDto.OrderType);
        }

        return true;
    }

    public async Task<bool> PrintStaffMealOrder(string staffName, List<TableItem> items)
    {
        // 1. Prepare OrderDto with ZERO prices (Staff meals are not for revenue)
        var orderDto = CreateSpecialOrderDto("Staff", staffName, "Staff Meal", "", items);
        orderDto.OrderNotice = $"(وجبة موظف: {staffName}) " + (_commonProperties.OrderNote ?? "");

        // 2. Create the order in DB
        orderDto.SkipPrintingOnServer = true;
        var result = await _orderSettingsService.CreateOrderAsync(orderDto);

        if (result == null) return false;

        // 3. Update POS state
        _commonProperties.CurrentOrderId = result.OrderId + 1;
        if (result.OrderDate.HasValue)
            _commonProperties.PosDate = DateOnly.FromDateTime(result.OrderDate.Value);

        try
        {
            // 4. Print ONLY to kitchen (No customer receipt for staff meals per request)
            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(orderDto, result, isReprint: true);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(orderDto, result, isReprint: true);
            }

            // 5. Record Usage in Database 
            if (_staffMealService != null && _commonProperties.CurrentStaffMeal != null)
            {
                var userId = _commonProperties.CurrentStaffMeal.UserId;
                foreach (var item in items)
                {
                    Log.Information("Recording usage for item {Item} (Qty: {Qty}) for user {User}", item.Name, item.Quantity, userId);
                    for (int i = 0; i < item.Quantity; i++)
                    {
                        var recordResult = await _staffMealService.RecordUsageAsync(new StaffMealUsageDto 
                        { 
                            UserId = userId ?? "Employee", 
                            ItemId = item.Id.ToString(), 
                            UsageDate = DateTime.Now,
                            OrderId = result.OrderId.ToString()
                        });
                        Log.Information("RecordUsageAsync result: {Result}", recordResult);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print staff meal for {Name}", staffName);
        }

        return true;
    }

    public async Task<bool> PrintHospitalityOrder(string responsibleName, string reason)
    {
        var items = _commonProperties.TableItems?.ToList() ?? new List<TableItem>();
        var orderDto = CreateSpecialOrderDto("Hospitality", responsibleName, reason, "", items);
        orderDto.OrderNotice = $"(ضيافة: {responsibleName}) - السبب: {reason} " + (_commonProperties.OrderNote ?? "");

        // Create the order in DB
        orderDto.SkipPrintingOnServer = true;
        var result = await _orderSettingsService.CreateOrderAsync(orderDto);

        if (result == null) return false;

        // Update POS state
        _commonProperties.CurrentOrderId = result.OrderId + 1;
        if (result.OrderDate.HasValue)
            _commonProperties.PosDate = DateOnly.FromDateTime(result.OrderDate.Value);

        try
        {
            // For hospitality, we print both Kitchen AND Customer receipts
            await PrintTakeAwayLocally(orderDto, result);

            var orderSettings = _commonProperties.OrderSettings?.FirstOrDefault(o => o.OrderType == "TakeAway");
            if (orderSettings != null)
            {
                if (orderSettings.FullKitchenReceiptCount > 0)
                    await PrintBackupReceiptsLocally(orderDto, result, isReprint: true);

                if (orderSettings.SeparateReceiptCount > 0)
                    await PrintKitchenReceiptsLocally(orderDto, result, isReprint: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print hospitality order for {Name}", responsibleName);
        }

        return true;
    }
}