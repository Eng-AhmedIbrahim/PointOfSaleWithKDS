namespace POS.Reports;

public interface IReportsManager
{
    Task<ReportResponseDto> GenerateReport(ReportRequestDto request);
}

public class ReportsManager : IReportsManager
{
    private readonly IFastReportService _fastReportService;
    private readonly IReportingService _reportingService;
    private readonly IInventoryService _inventoryService;
    private readonly IBranchService _branchService;
    private readonly IRecipeService _recipeService;
    private readonly IUnitOfWork _unitOfWork;

    public ReportsManager(IFastReportService fastReportService, 
        IReportingService reportingService, 
        IInventoryService inventoryService, 
        IBranchService branchService,
        IRecipeService recipeService,
        IUnitOfWork unitOfWork)
    {
        _fastReportService = fastReportService;
        _reportingService = reportingService;
        _inventoryService = inventoryService;
        _branchService = branchService;
        _recipeService = recipeService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReportResponseDto> GenerateReport(ReportRequestDto request)
    {
        var branches = await _branchService.GetBranchesAsync();
        var firstBranch = branches?.FirstOrDefault();
        string branchName = firstBranch?.Name ?? "Main Branch";
        string currency = firstBranch?.Currency ?? "EGP";
        
        if (request.Filters != null && request.Filters.TryGetValue("BranchName", out var filterBranch))
            branchName = filterBranch;

        if (request.Format?.ToUpper() == "EXCEL" && 
            request.ReportId != "SalesSummary" && 
            request.ReportId != "SalesDetailed" && 
            request.ReportId != "HospitalityReport" && 
            request.ReportId != "Hospitality" && 
            request.ReportId != "DetailedVoidReport" && 
            request.ReportId != "VoidedOrders" &&
            request.ReportId != "BranchComparison" &&
            request.ReportId != "DeliveryBonusReport" &&
            request.ReportId != "CashierComparison" &&
            request.ReportId != "DetailedDiscountsReport" &&
            request.ReportId != "CustomerDirectory" &&
            request.ReportId != "VipCustomers" &&
            request.ReportId != "DormantCustomers" &&
            request.ReportId != "DeliveryOrders" &&
            request.ReportId != "DeliveryAreas")
        {
            DataSet dsExcel = await FetchReportData(request);
            return GenerateExcelReport(request, dsExcel);
        }


        if (request.ReportId == "SalesSummary")
        {
            var summary = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
            var catItems = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
            

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportSalesSummary(summary, catItems, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"SalesSummary_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeSalesSummaryDocument(summary, catItems, branchName, request.FromDate, request.ToDate, isThermal, request.Language);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"SalesSummary_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "SalesDetailed")
        {
            string? orderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
            string? staffId = request.Filters != null && request.Filters.ContainsKey("StaffId") ? request.Filters["StaffId"] : null;
            string? staffType = request.Filters != null && request.Filters.ContainsKey("StaffType") ? request.Filters["StaffType"] : null;

            List<POS.Contract.Dtos.OrderDtos.OrderDto> orders;
            if (!string.IsNullOrEmpty(staffId) && !string.IsNullOrEmpty(staffType))
                orders = await _reportingService.GetStaffOrdersAsync(request.FromDate, staffId, staffType);
            else
                orders = await _reportingService.GetTodayOrdersAsync(request.FromDate, orderType);


            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportSalesDetailed(orders, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"SalesDetailed_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeSalesDetailedDocument(orders, branchName, request.FromDate, request.ToDate, request.Language, isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"SalesDetailed_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "ItemSales" || request.ReportId == "TopSellingItems")
        {
            var itemSummary = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
            if (request.ReportId == "TopSellingItems")
                itemSummary = itemSummary.OrderByDescending(i => i.Quantity).Take(20).ToList();


            string title = request.ReportId == "TopSellingItems" ? "الأصناف الأكثر مبيعاً" : "تقرير مبيعات الأصناف";
            if (request.Language?.ToLower() == "en")
                title = request.ReportId == "TopSellingItems" ? "Top Selling Items" : "Item Sales Report";

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            var document = new BackOfficeItemSalesDocument(itemSummary, branchName, request.FromDate, request.ToDate, title, request.Language, isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "CategoryAnalysis")
        {
            var items = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
            var orders = await _reportingService.GetTodayOrdersAsync(request.FromDate); // Or get date range orders if needed

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            var document = new BackOfficeCategoryAnalysisDocument(items, orders, branchName, request.FromDate, request.ToDate, isThermal, request.Language);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"CategoryAnalysis_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "VoidedOrders")
        {
            string? vOrderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
            var allOrdersForVoids = await _reportingService.GetTodayOrdersAsync(request.FromDate, vOrderType);
            
            var voidOrders = allOrdersForVoids.Where(o => (o.VoidAmount ?? 0) > 0 || o.OrderState == "Voided" || (o.OrderDetails?.Any(i => i.IsVoided) ?? false)).ToList();

            bool isThermal = IsThermalRequest(request);
            string title = (request.Language?.ToLower() == "en") ? "Void Transactions Summary Report" : "تقرير ملخص عمليات الحذف (Void)";

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportVoidSummary(voidOrders, branchName, request.FromDate, request.ToDate, request.Language ?? "ar");
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"VoidSummary_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeVoidSummaryDocument(voidOrders, branchName, request.FromDate, request.ToDate, title, request.Language ?? "ar", isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"VoidSummary_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DetailedVoidReport")
        {
            string? vOrderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
            var allOrdersForVoids = await _reportingService.GetTodayOrdersAsync(request.FromDate, vOrderType);
            
            var voidOrders = allOrdersForVoids.Where(o => (o.VoidAmount ?? 0) > 0 || o.OrderState == "Voided" || (o.OrderDetails?.Any(i => i.IsVoided) ?? false)).ToList();

            string cashierNameFilter = "";
            if (request.Filters != null && request.Filters.TryGetValue("CashierName", out var cName) && !string.IsNullOrWhiteSpace(cName) && cName != "All" && cName != "الكل")
            {
                cashierNameFilter = cName;
                voidOrders = voidOrders.Where(o =>
                    (o.CashierName != null && o.CashierName.Equals(cName, StringComparison.OrdinalIgnoreCase)) ||
                    (o.StaffName != null && o.StaffName.Equals(cName, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            else if (request.Filters != null && request.Filters.TryGetValue("CashierId", out var cId) && !string.IsNullOrWhiteSpace(cId) && cId != "All")
            {
                voidOrders = voidOrders.Where(o => o.CashierId == cId).ToList();
            }

            bool isThermal = IsThermalRequest(request);
            string title = (request.Language?.ToLower() == "en") ? "Detailed Void Report" : "تقرير المحذوفات التفصيلي للأصناف";
            if (!string.IsNullOrWhiteSpace(cashierNameFilter))
                title += $" - كاشير: {cashierNameFilter}";

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportDetailedVoids(voidOrders, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"DetailedVoidReport_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeVoidReportDocument(voidOrders.Cast<POS.Contract.Dtos.OrderDtos.OrderDto>().ToList(), branchName, request.FromDate, request.ToDate, title, request.Language, isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"DetailedVoidReport_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DiscountedOrders")
        {
            // GetTodayOrdersAsync only covers a single day; for date ranges use GetSalesItemsSummary query path.
            // TotalOrderDiscount is the correctly mapped field (from Orders.Discount via AutoMapper, line 211 in MappingProfiles)
            // TotalDiscount in OrderDto is NOT mapped from DB - so we check TotalOrderDiscount and DiscountedItems
            var allOrders = await _reportingService.GetTodayOrdersAsync(request.FromDate);
            
            // Also try to fetch the ToDate if different from FromDate by iterating through range
            if (request.ToDate.Date != request.FromDate.Date)
            {
                var currentDate = request.FromDate.Date.AddDays(1);
                while (currentDate <= request.ToDate.Date)
                {
                    var dayOrders = await _reportingService.GetTodayOrdersAsync(currentDate);
                    allOrders.AddRange(dayOrders);
                    currentDate = currentDate.AddDays(1);
                }
                allOrders = allOrders.DistinctBy(o => o.Id).ToList();
            }

            var discounts = allOrders.Where(o =>
                (o.TotalOrderDiscount ?? 0) > 0 ||
                (o.TotalDiscount ?? 0) > 0 ||
                (o.DiscountedItems ?? 0) > 0
            ).ToList();

            var document = new BackOfficeDiscountReportDocument(discounts.Cast<POS.Contract.Dtos.OrderDtos.OrderDto>().ToList(), branchName, request.FromDate, request.ToDate, request.Language, currency);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"DiscountedOrders_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DetailedDiscountsReport")
        {
            var allOrders = await _reportingService.GetTodayOrdersAsync(request.FromDate);
            if (request.ToDate.Date != request.FromDate.Date)
            {
                var currentDate = request.FromDate.Date.AddDays(1);
                while (currentDate <= request.ToDate.Date)
                {
                    var dayOrders = await _reportingService.GetTodayOrdersAsync(currentDate);
                    allOrders.AddRange(dayOrders);
                    currentDate = currentDate.AddDays(1);
                }
                allOrders = allOrders.DistinctBy(o => o.Id).ToList();
            }

            var discountOrders = allOrders.Where(o =>
                (o.TotalOrderDiscount ?? 0) > 0 ||
                (o.TotalDiscount ?? 0) > 0 ||
                (o.DiscountedItems ?? 0) > 0 ||
                (o.OrderDetails != null && o.OrderDetails.Any(i => i.HasDiscount || (i.DiscountAmount ?? 0) > 0 || (i.DiscountPercentage ?? 0) > 0 || (i.TotalDiscountPrice ?? 0) > 0))
            ).ToList();

            string cashierNameFilter = "";
            if (request.Filters != null && request.Filters.TryGetValue("CashierName", out var cName) && !string.IsNullOrWhiteSpace(cName) && cName != "All" && cName != "الكل")
            {
                cashierNameFilter = cName;
                discountOrders = discountOrders.Where(o => 
                    (o.CashierName != null && o.CashierName.Equals(cashierNameFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (o.StaffName != null && o.StaffName.Equals(cashierNameFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (o.DiscountByName != null && o.DiscountByName.Equals(cashierNameFilter, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            string title = request.Language?.ToLower() == "en" ? "Detailed Discounts Report" : "تقرير الخصومات التفصيلي";
            if (!string.IsNullOrWhiteSpace(cashierNameFilter))
                title += request.Language?.ToLower() == "en" ? $" - Cashier: {cashierNameFilter}" : $" - الكاشير: {cashierNameFilter}";

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportDetailedDiscounts(discountOrders, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"DetailedDiscounts_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeDetailedDiscountDocument(discountOrders.Cast<POS.Contract.Dtos.OrderDtos.OrderDto>().ToList(), branchName, request.FromDate, request.ToDate, title, request.Language, isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"DetailedDiscounts_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "InventoryStatus" || request.ReportId == "InventoryDetailed")
        {
            var invItems = await _inventoryService.GetAllInventoryItemsAsync();
            var dtoList = invItems.Select(i => new InventorySummaryDto
            {
                Id = i.MenuSalesItemId,
                ItemNameAr = i.ItemNameAr ?? i.MenuSalesItem?.ArabicName ?? "Unknown",
                ItemNameEn = i.ItemNameEn ?? i.MenuSalesItem?.EnglishName ?? "Unknown",
                CategoryNameAr = i.CategoryNameAr ?? "General",
                CategoryNameEn = i.CategoryNameEn ?? "General",
                UnitNameAr = i.UnitNameAr ?? "Unit",
                UnitNameEn = i.UnitNameEn ?? "Unit",
                CurrentQuantity = i.CurrentQuantity,
                MinimumQuantity = i.MinimumQuantity,
                TrackInventory = i.TrackInventory
            }).ToList();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeInventoryReportDocument(dtoList, branchName, DateTime.Now, "تقرير حالة المخزن", request.Language, isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto { Content = pdfBytes, ContentType = "application/pdf", FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "HospitalityReport")
        {
            var orders = await _reportingService.GetTodayOrdersAsync(request.FromDate, "Hospitality");

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportHospitality(orders, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"Hospitality_{request.FromDate:yyyyMMdd}.xlsx"
                };
            }

            var document = new BackOfficeHospitalityDocument(orders, branchName, request.FromDate, request.ToDate, isThermal, request.Language);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"Hospitality_{request.FromDate:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "StaffMeals")
        {
            var orders = await _reportingService.GetTodayOrdersAsync(request.FromDate, "Staff");

            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }

            var document = new StaffMealsDocument(orders, request.FromDate, branchName, (request.Language ?? "ar") == "ar", isThermal);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"StaffMeals_{request.FromDate:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "StockMovementReport")
        {
            var dsStock = await FetchReportData(request);
            var transactions = dsStock.Tables["Transactions"].AsEnumerable().Select(r => new {
                ItemName = r["ItemName"] == DBNull.Value ? "" : r["ItemName"].ToString(),
                TimeDisplay = r["TimeDisplay"] == DBNull.Value ? "" : r["TimeDisplay"].ToString(),
                Type = r["Type"] == DBNull.Value ? "" : r["Type"].ToString(),
                QtyChange = r["QtyChange"] == DBNull.Value ? 0m : Convert.ToDecimal(r["QtyChange"]),
                ResultQty = r["ResultQty"] == DBNull.Value ? 0m : Convert.ToDecimal(r["ResultQty"])
            }).ToList<dynamic>();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeStockMovementDocument(transactions, branchName, request.FromDate, request.ToDate, request.Language, isThermal);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"StockMove_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "WasteReport")
        {
            var dsWaste = await FetchReportData(request);
            var transactions = dsWaste.Tables["Transactions"].AsEnumerable().Select(r => new {
                ItemName = r["ItemName"],
                TimeDisplay = r["TimeDisplay"],
                QtyChange = r["QtyChange"],
                Reason = r["Reason"],
                Notes = r["Notes"]
            }).ToList();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeWasteReportDocument(transactions, branchName, request.FromDate, request.ToDate, request.Language, isThermal);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"Waste_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "LowStockReport")
        {
            var dsLow = await FetchReportData(request);
            var inv = dsLow.Tables["Inventory"].AsEnumerable().Select(r => new {
                ItemName = r["ItemName"] == DBNull.Value ? "" : r["ItemName"].ToString(),
                CurrentQty = r["CurrentQty"] == DBNull.Value ? 0m : Convert.ToDecimal(r["CurrentQty"]),
                MinQty = r["MinQty"] == DBNull.Value ? 0m : Convert.ToDecimal(r["MinQty"]),
                Unit = r["Unit"] == DBNull.Value ? "" : r["Unit"].ToString()
            }).ToList<dynamic>();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeLowStockReportDocument(inv, branchName, DateTime.Now, request.Language, isThermal);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"LowStock_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "RecipeReport")
        {
            var dsRecipe = await FetchReportData(request);
            var recipes = dsRecipe.Tables["Recipes"].AsEnumerable().Select(r => new {
                ProductName = r["ProductName"],
                RecipeName = r["RecipeName"],
                IngredientName = r["IngredientName"],
                Quantity = r["Quantity"],
                Unit = r["Unit"]
            }).ToList();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeRecipeReportDocument(recipes, branchName, request.Language, isThermal);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"Recipes_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "StockTakeReport")
        {
            var dsTake = await FetchReportData(request);
            var inv = dsTake.Tables["Inventory"].AsEnumerable().Select(r => new {
                Id = 0, // Id is not available in InventoryToDataTable
                ItemName = r["ItemName"] == DBNull.Value ? "" : r["ItemName"].ToString(),
                Category = r["Category"] == DBNull.Value ? "" : r["Category"].ToString(),
                Unit = r["Unit"] == DBNull.Value ? "" : r["Unit"].ToString(),
                SystemQty = r["CurrentQty"] == DBNull.Value ? 0m : Convert.ToDecimal(r["CurrentQty"])
            }).ToList<dynamic>();

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeStockTakeFormDocument(inv, branchName, request.Language, isThermal);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"StockTakeForm_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "PnLReport" || request.ReportId == "ProfitAndLoss")
        {
            var summary = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficePnLDocument(summary, branchName, request.FromDate, request.ToDate, isThermal, request.Language);
            return new ReportResponseDto { Content = document.GeneratePdf(), ContentType = "application/pdf", FileName = $"PnL_{request.FromDate:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "CustomerDirectory" || request.ReportId == "VipCustomers" || request.ReportId == "DormantCustomers")
        {
            var customerRepo = _unitOfWork.Repository<POS.Core.Entities.Delivery.DeliveryCustomerInfo>();
            var allCustomers = await customerRepo.GetAllAsync();
            var ordersRepo = _unitOfWork.Repository<POS.Core.Entities.OrderEntity.Orders>();
            var allOrders = await ordersRepo.GetAllAsync();
            
            int dormantDays = 30;
            if (request.Filters != null && request.Filters.TryGetValue("DormantDays", out var ddStr) && int.TryParse(ddStr, out var pDormant))
                dormantDays = pDormant;

            var customersData = new List<CustomerReportItem>();
            foreach (var cust in allCustomers)
            {
                var cOrders = allOrders.Where(o => o.Phone1 == cust.FirstPhoneNumber || o.Phone2 == cust.FirstPhoneNumber).ToList();
                var lastOrder = cOrders.OrderByDescending(o => o.OrderDate).FirstOrDefault();

                customersData.Add(new CustomerReportItem
                {
                    Name = cust.CustomerName ?? "-",
                    Phone = cust.FirstPhoneNumber ?? "-",
                    Address = cust.CustomerAddresses?.FirstOrDefault()?.ClientAddress ?? cust.CustomerAddresses?.FirstOrDefault()?.AddressNote ?? "-",
                    TotalOrders = cOrders.Count,
                    TotalAmount = cOrders.Sum(o => o.GrandTotal ?? 0),
                    LastOrderDate = lastOrder?.OrderDate
                });
            }

            if (request.ReportId == "VipCustomers")
                customersData = customersData.Where(c => c.TotalOrders > 0).OrderByDescending(c => c.TotalOrders).Take(50).ToList();
            else if (request.ReportId == "DormantCustomers")
            {
                // Dormant = customers whose last order is older than (ToDate - dormantDays)
                // OR customers who never ordered at all
                var thresholdDate = request.ToDate.Date.AddDays(-dormantDays);
                customersData = customersData
                    .Where(c => !c.LastOrderDate.HasValue || c.LastOrderDate.Value.Date < thresholdDate)
                    .OrderBy(c => c.LastOrderDate ?? DateTime.MinValue)
                    .ToList();
            }
            else
            {
                customersData = customersData.OrderByDescending(c => c.LastOrderDate).ToList();
            }

            bool isThermal = IsThermalRequest(request);

            string title = request.ReportId == "VipCustomers" ? "العملاء الأكثر طلباً (VIP)" :
                           request.ReportId == "DormantCustomers" ? $"العملاء المنقطعين (أكثر من {dormantDays} يوم)" :
                           "دليل خدمة العملاء";
                           
            if (request.Language?.ToLower() == "en")
            {
               title = request.ReportId == "VipCustomers" ? "VIP Customers" :
                       request.ReportId == "DormantCustomers" ? $"Dormant Customers (>{dormantDays} days)" :
                       "Customer Directory";
            }

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportCustomerReport(customersData, branchName, title, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }
            var document = new POS.Reports.ReportsMakerServices.BackOfficeCustomerReportDocument(customersData, branchName, title, request.FromDate, request.ToDate, isThermal, request.Language);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto
            {
                Content = pdfBytes,
                ContentType = "application/pdf",
                FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DeliveryOrders")
        {
            var allOrders = await _reportingService.GetTodayOrdersAsync(request.FromDate, "Delivery");
            bool isThermal = IsThermalRequest(request);
            string title = "تقرير طلبات التوصيل";
            if (request.Language?.ToLower() == "en") title = "Delivery Orders Report";
            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportSalesDetailed(allOrders, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"DeliveryOrders_{request.FromDate:yyyyMMdd}.xlsx"
                };
            }
            var document = new BackOfficeSalesDetailedDocument(allOrders, branchName, request.FromDate, request.ToDate, request.Language, isThermal);
            return new ReportResponseDto
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"DeliveryOrders_{request.FromDate:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DeliveryAreas")
        {
            var ordersRepo = _unitOfWork.Repository<POS.Core.Entities.OrderEntity.Orders>();
            var allOrders = await ordersRepo.GetAllAsync();
            var deliveryOrders = allOrders.Where(o => o.OrderDate >= request.FromDate.Date && o.OrderDate <= request.ToDate.Date.AddDays(1).AddTicks(-1) && o.OrderState != POS.Core.Entities.OrderEntity.OrderStates.Voided && o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.Delivery).ToList();

            var areaData = deliveryOrders
                .GroupBy(o => string.IsNullOrWhiteSpace(o.ZoneName) ? "غير محدد" : o.ZoneName)
                .Select(g => new POS.Reports.ReportsMakerServices.DeliveryAreaReportItem
                {
                    ZoneName = g.Key,
                    TotalOrders = g.Count(),
                    TotalAmount = g.Sum(o => o.GrandTotal ?? 0)
                })
                .OrderByDescending(a => a.TotalOrders)
                .ToList();

            bool isThermal = IsThermalRequest(request);
            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportDeliveryAreas(areaData, branchName, request.FromDate, request.ToDate, request.Language);
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"DeliveryAreas_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }
            var document = new POS.Reports.ReportsMakerServices.BackOfficeDeliveryAreasDocument(areaData, branchName, request.FromDate, request.ToDate, isThermal, request.Language);
            
            return new ReportResponseDto
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"DeliveryAreas_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "HourlySales")
        {
            var summary = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
            var hourlyData = summary.Overall.HourlySales;

            bool isThermal = IsThermalRequest(request);
            var document = new BackOfficeHourlySalesDocument(hourlyData, branchName, request.FromDate, isThermal, request.Language, currency);
            byte[] pdfBytes = document.GeneratePdf();

            return new ReportResponseDto { Content = pdfBytes, ContentType = "application/pdf", FileName = $"HourlySales_{DateTime.Now:yyyyMMdd}.pdf" };
        }

        if (request.ReportId == "BranchComparison")
        {
            var ordersRepo = _unitOfWork.Repository<POS.Core.Entities.OrderEntity.Orders>();
            var allOrders = await ordersRepo.GetAllAsync();
            var toDateInclusive = request.ToDate.Date.AddDays(1).AddTicks(-1);
            var filteredOrders = allOrders.Where(o =>
                o.OrderDate >= request.FromDate.Date &&
                o.OrderDate <= toDateInclusive &&
                o.OrderState != POS.Core.Entities.OrderEntity.OrderStates.Voided
            ).ToList();

            var branchesList = await _branchService.GetBranchesAsync();
            var branchDict = branchesList?.ToDictionary(b => b.Id, b => b.Name) ?? new();

            var grouped = filteredOrders.GroupBy(o => o.BranchID).ToList();
            var branchItems = new List<POS.Reports.ReportsMakerServices.BranchComparisonItem>();

            foreach (var g in grouped)
            {
                string bName = "";
                if (branchDict.TryGetValue(g.Key, out var foundName) && !string.IsNullOrWhiteSpace(foundName))
                    bName = foundName;
                else
                    bName = g.FirstOrDefault()?.BranchName ?? $"فرع #{g.Key}";

                var bOrders = g.ToList();
                decimal total = bOrders.Sum(o => o.GrandTotal ?? 0);
                var dineOrders = bOrders.Where(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.DineIn).ToList();
                var takeOrders = bOrders.Where(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.TakeAway).ToList();
                var delivOrders = bOrders.Where(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.Delivery).ToList();

                branchItems.Add(new POS.Reports.ReportsMakerServices.BranchComparisonItem
                {
                    BranchId = g.Key,
                    BranchName = bName,
                    OrderCount = bOrders.Count,
                    TotalSales = total,
                    DineInCount = dineOrders.Count,
                    DineInTotal = dineOrders.Sum(o => o.GrandTotal ?? 0),
                    TakeawayCount = takeOrders.Count,
                    TakeawayTotal = takeOrders.Sum(o => o.GrandTotal ?? 0),
                    DeliveryCount = delivOrders.Count,
                    DeliveryTotal = delivOrders.Sum(o => o.GrandTotal ?? 0)
                });
            }

            if (branchesList != null)
            {
                foreach (var b in branchesList)
                {
                    if (!branchItems.Any(x => x.BranchId == b.Id))
                    {
                        branchItems.Add(new POS.Reports.ReportsMakerServices.BranchComparisonItem
                        {
                            BranchId = b.Id,
                            BranchName = b.Name,
                            OrderCount = 0,
                            TotalSales = 0
                        });
                    }
                }
            }

            decimal grandTotal = branchItems.Sum(b => b.TotalSales);
            decimal maxSales = branchItems.Any() ? branchItems.Max(b => b.TotalSales) : 0;
            foreach (var item in branchItems)
            {
                item.PercentageOfTotal = grandTotal > 0 ? (item.TotalSales / grandTotal) * 100 : 0;
                item.IsTopBranch = item.TotalSales == maxSales && maxSales > 0;
            }

            branchItems = branchItems.OrderByDescending(b => b.TotalSales).ToList();

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportBranchComparison(branchItems, branchName, request.FromDate, request.ToDate, request.Language ?? "ar");
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"BranchComparison_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            bool isThermal = IsThermalRequest(request);
            var document = new POS.Reports.ReportsMakerServices.BackOfficeBranchComparisonDocument(
                branchItems, branchName, request.FromDate, request.ToDate, isThermal, request.Language);

            return new ReportResponseDto
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"BranchComparison_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "DeliveryBonusReport")
        {
            var ordersRepo = _unitOfWork.Repository<POS.Core.Entities.OrderEntity.Orders>();
            var allOrders = await ordersRepo.GetAllAsync();
            var toDateInclusive = request.ToDate.Date.AddDays(1).AddTicks(-1);
            var deliveryOrders = allOrders.Where(o =>
                o.OrderDate >= request.FromDate.Date &&
                o.OrderDate <= toDateInclusive &&
                o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.Delivery &&
                o.OrderState != POS.Core.Entities.OrderEntity.OrderStates.Voided
            ).ToList();

            var orderRecords = deliveryOrders.Select(o => new POS.Reports.ReportsMakerServices.DeliveryOrderRecord
            {
                OrderId = o.OrderID > 0 ? o.OrderID : o.Id,
                OrderDate = o.OrderDate,
                CustomerName = o.CustomerName ?? "",
                Phone = o.Phone1 ?? o.Phone2 ?? "",
                ZoneName = o.ZoneName ?? "",
                DriverName = !string.IsNullOrWhiteSpace(o.DriverName) ? o.DriverName : "غير محدد",
                DeliveryFees = o.DeliveryFees ?? 0,
                Bonus = o.ZoneBonus ?? 0,
                GrandTotal = o.GrandTotal ?? 0
            }).OrderByDescending(o => o.OrderDate).ToList();

            var driverGroups = deliveryOrders
                .GroupBy(o => !string.IsNullOrWhiteSpace(o.DriverName) ? o.DriverName : (!string.IsNullOrWhiteSpace(o.DriverID) ? o.DriverID : "غير محدد"))
                .ToList();

            decimal grandBonus = deliveryOrders.Sum(o => o.ZoneBonus ?? 0);
            var driverItems = new List<POS.Reports.ReportsMakerServices.DriverBonusItem>();

            foreach (var g in driverGroups)
            {
                var dOrders = g.ToList();
                decimal totalBonus = dOrders.Sum(o => o.ZoneBonus ?? 0);
                driverItems.Add(new POS.Reports.ReportsMakerServices.DriverBonusItem
                {
                    DriverId = dOrders.FirstOrDefault()?.DriverID ?? "",
                    DriverName = g.Key,
                    OrderCount = dOrders.Count,
                    TotalSales = dOrders.Sum(o => o.GrandTotal ?? 0),
                    DeliveryFees = dOrders.Sum(o => o.DeliveryFees ?? 0),
                    TotalBonus = totalBonus,
                    BonusPercentage = grandBonus > 0 ? (totalBonus / grandBonus) * 100 : 0
                });
            }

            decimal maxBonus = driverItems.Any() ? driverItems.Max(d => d.TotalBonus) : 0;
            foreach (var d in driverItems)
            {
                d.IsTopDriver = d.TotalBonus == maxBonus && maxBonus > 0;
            }

            driverItems = driverItems.OrderByDescending(d => d.TotalBonus).ToList();

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportDeliveryBonus(driverItems, orderRecords, branchName, request.FromDate, request.ToDate, request.Language ?? "ar");
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"DeliveryBonus_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            bool isThermal = IsThermalRequest(request);
            var document = new POS.Reports.ReportsMakerServices.BackOfficeDeliveryBonusDocument(
                driverItems, orderRecords, branchName, request.FromDate, request.ToDate, isThermal, request.Language);

            return new ReportResponseDto
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"DeliveryBonus_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        if (request.ReportId == "CashierComparison")
        {
            var ordersRepo = _unitOfWork.Repository<POS.Core.Entities.OrderEntity.Orders>();
            var allOrders = await ordersRepo.GetAllAsync();
            var toDateInclusive = request.ToDate.Date.AddDays(1).AddTicks(-1);

            var dateOrders = allOrders.Where(o =>
                (o.OrderDate >= request.FromDate.Date && o.OrderDate <= toDateInclusive) ||
                (o.VoidTime.HasValue && o.VoidTime.Value.Date >= request.FromDate.Date && o.VoidTime.Value.Date <= request.ToDate.Date)
            ).ToList();

            var cashierGroups = dateOrders
                .Where(o => !string.IsNullOrEmpty(o.CashierID) || !string.IsNullOrEmpty(o.CashierName))
                .GroupBy(o => !string.IsNullOrEmpty(o.CashierID) ? o.CashierID : o.CashierName)
                .ToList();

            var cashierItems = new List<POS.Reports.ReportsMakerServices.CashierComparisonItem>();

            foreach (var g in cashierGroups)
            {
                var cOrders = g.ToList();
                var completed = cOrders.Where(o => o.OrderState == POS.Core.Entities.OrderEntity.OrderStates.Completed).ToList();
                var first = cOrders.FirstOrDefault();
                string cName = !string.IsNullOrWhiteSpace(first?.CashierName) ? first.CashierName : g.Key!;

                decimal sales = completed.Sum(o => o.GrandTotal ?? 0);
                decimal cash = completed.Where(o => o.PaymentMethod == POS.Contract.Models.PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0);
                decimal credit = completed.Where(o => o.PaymentMethod != POS.Contract.Models.PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0);
                decimal discount = cOrders.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0)));
                decimal voidAmt = cOrders.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == POS.Core.Entities.OrderEntity.OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0)));

                cashierItems.Add(new POS.Reports.ReportsMakerServices.CashierComparisonItem
                {
                    CashierId = g.Key!,
                    CashierName = cName,
                    OrderCount = cOrders.Count,
                    CompletedCount = completed.Count,
                    TotalSales = sales,
                    CashAmount = cash,
                    CreditAmount = credit,
                    TotalDiscount = discount,
                    DiscountCount = cOrders.Count(o => (o.TotalDiscount ?? 0) > 0 || (o.Discount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0),
                    TotalVoid = voidAmt,
                    VoidCount = cOrders.Count(o => o.OrderState == POS.Core.Entities.OrderEntity.OrderStates.Voided || (o.VoidAmount ?? 0) > 0 || (o.TotalVoid ?? 0) > 0),
                    TakeawayCount = cOrders.Count(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.TakeAway),
                    DeliveryCount = cOrders.Count(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.Delivery),
                    DineInCount = cOrders.Count(o => o.OrderType == POS.Core.Entities.OrderEntity.OrderTypes.DineIn)
                });
            }

            decimal totalAllCashiers = cashierItems.Sum(c => c.TotalSales);
            decimal maxCashierSales = cashierItems.Any() ? cashierItems.Max(c => c.TotalSales) : 0;
            int maxCashierOrders = cashierItems.Any() ? cashierItems.Max(c => c.OrderCount) : 0;

            foreach (var item in cashierItems)
            {
                item.PercentageOfTotal = totalAllCashiers > 0 ? (item.TotalSales / totalAllCashiers) * 100 : 0;
                item.IsTopSales = item.TotalSales == maxCashierSales && maxCashierSales > 0;
                item.IsTopOrders = item.OrderCount == maxCashierOrders && maxCashierOrders > 0;
            }

            cashierItems = cashierItems.OrderByDescending(c => c.TotalSales).ToList();

            if (request.Format?.ToUpper() == "EXCEL")
            {
                byte[] excelBytes = ExcelReportExportService.ExportCashierComparison(cashierItems, branchName, request.FromDate, request.ToDate, request.Language ?? "ar");
                return new ReportResponseDto
                {
                    Content = excelBytes,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"CashierComparison_{DateTime.Now:yyyyMMdd}.xlsx"
                };
            }

            bool isThermal = IsThermalRequest(request);
            var document = new POS.Reports.ReportsMakerServices.BackOfficeCashierComparisonDocument(
                cashierItems, branchName, request.FromDate, request.ToDate, isThermal, request.Language);

            return new ReportResponseDto
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"CashierComparison_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        DataSet ds = await FetchReportData(request);
        
        if (request.Format.ToUpper() == "EXCEL")
        {
            return GenerateExcelReport(request, ds);
        }
        else
        {
            string reportName = request.ReportId;
            bool isThermal = false;
            if (request.Filters != null)
            {
                var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
                var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
                foreach (var k in keysToCheck)
                {
                    if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        isThermal = true;
                        break;
                    }
                }
            }
            if (isThermal) reportName += "_Thermal";
            bool isEnglish = (request.Language ?? "ar").ToLower() == "en";
            string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            var candidateList = new List<string>();
            if (isEnglish)
            {
                if (isThermal) candidateList.Add($"{request.ReportId}_Thermal_EN");
                candidateList.Add($"{request.ReportId}_EN");
            }
            
            if (isThermal) candidateList.Add($"{request.ReportId}_Thermal");
            candidateList.Add(request.ReportId);
            candidateList.Add("GenericReport");

            string templatePath = candidateList
                .Select(n => Path.Combine(templatesDir, $"{n}.frx"))
                .FirstOrDefault(File.Exists)
                ?? Path.Combine(templatesDir, "GenericReport.frx");

            var frBranchName = branchName;
            if (request.Filters != null && request.Filters.TryGetValue("BranchName", out var frFilterBranch) && !string.IsNullOrWhiteSpace(frFilterBranch))
            {
                frBranchName = frFilterBranch;
            }

            string subTitle = "";
            if (request.Filters != null)
            {
                if (request.Filters.TryGetValue("OrderType", out var ot)) subTitle += isEnglish ? $"Type: {ot} " : $"النوع: {ot} ";
                if (request.Filters.TryGetValue("StaffName", out var sn)) subTitle += isEnglish ? $"Staff: {sn} " : $"الموظف: {sn} ";
                if (request.Filters.TryGetValue("OrderState", out var os)) subTitle += isEnglish ? $"Status: {os} " : $"الحالة: {os} ";
            }

            var parameters = new Dictionary<string, string>
            {
                { "FromDate", request.FromDate.ToString("yyyy-MM-dd") },
                { "ToDate", request.ToDate.ToString("yyyy-MM-dd") },
                { "ReportTitle", request.ReportId },
                { "BranchName", frBranchName },
                { "SubTitle", subTitle }
            };

            if (request.Filters != null)
            {
                foreach (var filter in request.Filters)
                {
                    if (!parameters.ContainsKey(filter.Key))
                        parameters[filter.Key] = filter.Value;
                }
            }

            byte[] content = _fastReportService.GeneratePdf(templatePath, ds, parameters);
            return new ReportResponseDto
            {
                Content = content,
                FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMddHHmmss}.pdf",
                ContentType = "application/pdf"
            };
        }
    }

    private async Task<DataSet> FetchReportData(ReportRequestDto request)
    {
        var ds = new DataSet("ReportData");
        bool isEnglish = (request.Language ?? "ar").ToLower() == "en";
        
        switch (request.ReportId)
        {
            case "SalesSummary":
                var summary = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
                var catItems = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
                
                ds.Tables.Add(await SummaryToDataTable(summary, request.FromDate, request.ToDate));
                ds.Tables.Add(GetSalesByTypeTable(summary.Overall.ModeDetails));
                ds.Tables.Add(GetEmployeeSalesTable(summary.DetailedOrders));
                ds.Tables.Add(GetTopItemsTable(catItems));
                ds.Tables.Add(GetVoidOrdersTable(summary.VoidEvents));
                ds.Tables.Add(GetDiscountOrdersTable(summary.DetailedOrders));
                
                // Keep old ones for compatibility with other templates
                ds.Tables.Add(CategorySummaryToDataTable(catItems, summary.Overall.TotalSales, summary.DetailedOrders));
                ds.Tables.Add(HourlySalesToDataTable(summary.Overall.HourlySales));
                ds.Tables.Add(ModeDetailsToDataTable(summary.Overall.ModeDetails));
                ds.Tables.Add(VoidEventsToDataTable(summary.VoidEvents, "VoidedOrders"));
                if (summary.DetailedOrders != null) ds.Tables.Add(OrdersToDataTable(summary.DetailedOrders, "DetailedOrders"));
                ds.Tables.Add(ItemsSummaryToDataTable(catItems));
                
                request.Filters ??= new Dictionary<string, string>();
                request.Filters["TotalItemsValue"] = catItems.Sum(x => x.TotalAmount).ToString("0.##");
                break;
            case "SalesDetailed":
                string? orderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
                string? staffId = request.Filters != null && request.Filters.ContainsKey("StaffId") ? request.Filters["StaffId"] : null;
                string? staffType = request.Filters != null && request.Filters.ContainsKey("StaffType") ? request.Filters["StaffType"] : null;

                List<POS.Contract.Dtos.OrderDtos.OrderDto> orders;
                if (!string.IsNullOrEmpty(staffId) && !string.IsNullOrEmpty(staffType))
                    orders = await _reportingService.GetStaffOrdersAsync(request.FromDate, staffId, staffType);
                else
                    orders = await _reportingService.GetTodayOrdersAsync(request.FromDate, orderType);
                ds.Tables.Add(OrdersToDataTable(orders));

                var staffSummaryDt = new DataTable("StaffSummary");
                staffSummaryDt.Columns.Add("StaffName");
                staffSummaryDt.Columns.Add("OrderCount", typeof(int));
                staffSummaryDt.Columns.Add("OrderAmount", typeof(decimal));
                staffSummaryDt.Columns.Add("VoidCount", typeof(int));
                staffSummaryDt.Columns.Add("VoidAmount", typeof(decimal));
                staffSummaryDt.Columns.Add("TotalDiscount", typeof(decimal));

                if (!string.IsNullOrEmpty(staffId))
                {
                    var isStaffAll = staffId.Equals("All", StringComparison.OrdinalIgnoreCase);
                    var grouped = orders.GroupBy(o => isStaffAll ? (staffType == "Waiter" ? o.WaiterName : (staffType == "Driver" ? o.DriverName : o.CashierName)) : (request.Filters.ContainsKey("StaffName") ? request.Filters["StaffName"] : "N/A"))
                        .Select(g => new {
                            Name = g.Key ?? "N/A",
                            CompOrders = g.Where(o => o.OrderState == "Completed"),
                            VoidOrders = g.Where(o => o.OrderState == "Voided" || (o.VoidAmount ?? 0) > 0)
                        }).ToList();

                    foreach(var g in grouped)
                    {
                        var oCnt = g.CompOrders.Count();
                        var oAmt = g.CompOrders.Sum(o => o.GrandTotal ?? 0);
                        var vCnt = g.VoidOrders.Count();
                        var vAmt = g.VoidOrders.Sum(o => o.VoidAmount ?? (o.OrderState == "Voided" ? o.GrandTotal : 0) ?? 0);
                        var disc = g.CompOrders.Sum(o => o.TotalDiscount ?? 0) + g.VoidOrders.Sum(o => o.TotalDiscount ?? 0);

                        staffSummaryDt.Rows.Add(g.Name, oCnt, oAmt, vCnt, vAmt, disc);
                    }
                }
                ds.Tables.Add(staffSummaryDt);
                
                var allItems = orders.SelectMany(o => (o.OrderDetails ?? new List<POS.Contract.Models.TableItem>()).Select(i => new { OrderId = o.OrderId, Item = i })).ToList();
                var itemsDt = new DataTable("OrderDetails");
                itemsDt.Columns.Add("OrderId");
                itemsDt.Columns.Add("ItemName");
                itemsDt.Columns.Add("Qty", typeof(decimal));
                itemsDt.Columns.Add("Price", typeof(decimal));
                itemsDt.Columns.Add("Total", typeof(decimal));
                itemsDt.Columns.Add("IsVoided", typeof(bool));
                itemsDt.Columns.Add("DiscountAmount", typeof(decimal));
                itemsDt.Columns.Add("TotalAfterDiscount", typeof(decimal));
                itemsDt.Columns.Add("VoidReason");
                itemsDt.Columns.Add("VoidByName");
                
                bool useEnglishNames = (request.Language ?? "ar").ToLower() == "en";
                foreach(var item in allItems)
                {
                    string displayName = useEnglishNames 
                        ? (string.IsNullOrEmpty(item.Item.Name) ? item.Item.NameAr : item.Item.Name)
                        : (string.IsNullOrEmpty(item.Item.NameAr) ? item.Item.Name : item.Item.NameAr);

                    itemsDt.Rows.Add(
                        item.OrderId,
                        displayName,
                        item.Item.Quantity,
                        item.Item.Price ?? 0,
                        (item.Item.Quantity * (item.Item.Price ?? 0)),
                        item.Item.IsVoided,
                        item.Item.DiscountAmount ?? 0,
                        item.Item.TotalAfterDiscount ?? (item.Item.Quantity * (item.Item.Price ?? 0)),
                        item.Item.VoidReason,
                        item.Item.VoidByName
                    );
                }
                ds.Tables.Add(itemsDt);
                break;
            case "HourlySales":
                var hSummary = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
                var hData = hSummary.Overall.HourlySales;
                if (request.Filters != null && request.Filters.TryGetValue("FromHour", out var fhStr) && int.TryParse(fhStr, out var fh) &&
                    request.Filters.TryGetValue("ToHour", out var thStr) && int.TryParse(thStr, out var th))
                {
                    hData = hData.Where(h => h.Hour >= fh && h.Hour <= th).ToList();
                }

                bool isEnglishReport = (request.Language ?? "ar").ToLower() == "en";
                if (isEnglishReport)
                {
                    foreach (var h in hData)
                    {
                        h.HourLabel = (h.Hour == 0) ? "12 AM" : (h.Hour == 12) ? "12 PM" : (h.Hour > 12) ? $"{h.Hour - 12} PM" : $"{h.Hour} AM";
                    }
                }

                var maxAmt = hData.Any() ? hData.Max(h => h.Amount) : 0;
                var maxAmtHour = hData.FirstOrDefault(h => h.Amount == maxAmt)?.HourLabel ?? "-";
                var maxCnt = hData.Any() ? hData.Max(h => h.OrderCount) : 0;
                var maxCntHour = hData.FirstOrDefault(h => h.OrderCount == maxCnt)?.HourLabel ?? "-";

                request.Filters ??= new Dictionary<string, string>();
                request.Filters["MaxAmount"] = maxAmt.ToString("0.##");
                request.Filters["MaxAmountHour"] = maxAmtHour;
                request.Filters["MaxCount"] = maxCnt.ToString();
                request.Filters["MaxCountHour"] = maxCntHour;

                ds.Tables.Add(HourlySalesToDataTable(hData));
                break;

            case "TopSellingItems":
                var topItems = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
                var sortedTop = topItems.OrderByDescending(i => i.Quantity).Take(20).ToList();
                ds.Tables.Add(ItemSummaryToDataTable(sortedTop));
                break;

            case "CategoryAnalysis":
                var catSummary = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
                var overall = await _reportingService.GetSalesSummaryAsync(request.FromDate, request.ToDate);
                ds.Tables.Add(CategorySummaryToDataTable(catSummary, overall.Overall.TotalSales, overall.DetailedOrders));
                break;

            case "CommentsReport":
                string? cOrderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
                var ordersForComments = await _reportingService.GetTodayOrdersAsync(request.FromDate, cOrderType);
                var commentsDt = new DataTable("Comments");
                commentsDt.Columns.Add("OrderId");
                commentsDt.Columns.Add("OrderDate", typeof(DateTime));
                commentsDt.Columns.Add("ItemName");
                commentsDt.Columns.Add("Comment");
                commentsDt.Columns.Add("Type");

                foreach (var ord in ordersForComments)
                {
                    if (!string.IsNullOrEmpty(ord.OrderNotice))
                    {
                        commentsDt.Rows.Add(ord.OrderId, ord.OrderDate, "ALL ORDER", ord.OrderNotice, "Order Notice");
                    }
                    foreach (var itm in ord.OrderDetails ?? new List<POS.Contract.Models.TableItem>())
                    {
                        if (!string.IsNullOrEmpty(itm.LineComment))
                            commentsDt.Rows.Add(ord.OrderId, ord.OrderDate, itm.NameAr ?? itm.Name, itm.LineComment, "Item Comment");
                    }
                }
                ds.Tables.Add(commentsDt);
                break;

            case "DetailedVoidReport":
                string? vOrderType = request.Filters != null && request.Filters.ContainsKey("OrderType") ? request.Filters["OrderType"] : null;
                var allOrdersForVoids = await _reportingService.GetTodayOrdersAsync(request.FromDate, vOrderType);
                var voidItemsDt = new DataTable("VoidedItems");
                voidItemsDt.Columns.Add("OrderId");
                voidItemsDt.Columns.Add("ItemName");
                voidItemsDt.Columns.Add("Qty", typeof(decimal));
                voidItemsDt.Columns.Add("Total", typeof(decimal));
                voidItemsDt.Columns.Add("VoidReason");
                voidItemsDt.Columns.Add("VoidByName");
                voidItemsDt.Columns.Add("VoidTime", typeof(DateTime));

                foreach (var ord in allOrdersForVoids)
                {
                    foreach (var itm in (ord.OrderDetails ?? new List<POS.Contract.Models.TableItem>()).Where(i => i.IsVoided))
                    {
                        voidItemsDt.Rows.Add(ord.OrderId, itm.NameAr ?? itm.Name, itm.Quantity, itm.Total, itm.VoidReason, itm.VoidByName, itm.VoidTime);
                    }
                }
                ds.Tables.Add(voidItemsDt);
                var orderVoids = allOrdersForVoids.Where(o => (o.VoidAmount ?? 0) > 0).ToList();
                ds.Tables.Add(OrdersToDataTable(orderVoids, "VoidedOrders"));
                break;

            case "ItemSales":
                var itemSummary = await _reportingService.GetSalesItemsSummaryAsync(request.FromDate, request.ToDate);
                ds.Tables.Add(ItemSummaryToDataTable(itemSummary));
                break;
            case "InventoryStatus":
            case "InventoryDetailed":
            case "StockTakeReport":
                var invItems = await _inventoryService.GetAllInventoryItemsAsync();
                ds.Tables.Add(InventoryToDataTable(invItems));
                break;
            case "LowStockReport":
                var allInv = await _inventoryService.GetAllInventoryItemsAsync();
                var lowStock = allInv.Where(i => i.CurrentQuantity <= i.MinimumQuantity && i.TrackInventory).ToList();
                ds.Tables.Add(InventoryToDataTable(lowStock));
                break;
            case "StockMovementReport":
                var mid = 0;
                if (request.Filters != null && request.Filters.TryGetValue("ItemId", out var idStr))
                {
                    int.TryParse(idStr, out mid);
                }

                var moveSpec = new InventoryTransactionsSpecification(request.FromDate, request.ToDate, mid > 0 ? mid : null);
                var transList = await _unitOfWork.Repository<InventoryTransaction>().GetAllWithSpecificationAsync(moveSpec);
                var transactions = transList.ToList();
                
                ds.Tables.Add(TransactionsToDataTable(transactions, isEnglish));
                break;
            case "WasteReport":
                var wasteSpec = new WasteAndDamageTransactionsSpecification(request.FromDate, request.ToDate);
                var wasteTrans = await _unitOfWork.Repository<InventoryTransaction>().GetAllWithSpecificationAsync(wasteSpec);
                ds.Tables.Add(TransactionsToDataTable(wasteTrans.ToList(), isEnglish));
                break;
            case "RecipeReport":
                var recipesEn = (await _recipeService.GetAllRecipesAsync())
                    .OrderBy(r => isEnglish ? (r.MenuSalesItem?.EnglishName ?? r.MenuSalesItem?.ArabicName) : r.MenuSalesItem?.ArabicName)
                    .ThenBy(r => r.RecipeName)
                    .ToList();
                ds.Tables.Add(RecipesToDataTable(recipesEn, isEnglish));
                break;
            case "VoidedOrders":
                var allForVoids = await _reportingService.GetTodayOrdersAsync(request.FromDate);
                var voids = allForVoids.Where(o => (o.VoidAmount ?? 0) > 0).ToList();
                ds.Tables.Add(OrdersToDataTable(voids));
                break;
            case "DiscountedOrders":
            case "DetailedDiscountsReport":
                var allForDiscounts = await _reportingService.GetTodayOrdersAsync(request.FromDate);
                var discounts = allForDiscounts.Where(o => (o.TotalDiscount ?? 0) > 0 || (o.TotalOrderDiscount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0).ToList();
                ds.Tables.Add(OrdersToDataTable(discounts));
                break;
        }

        return ds;
    }

    private DataTable CategorySummaryToDataTable(List<SalesItemSummaryDto> items, decimal totalSales, List<POS.Contract.Dtos.OrderDtos.OrderDto>? orders = null)
    {
        var dt = new DataTable("Categories");
        dt.Columns.Add("CategoryName");
        dt.Columns.Add("OrderCount", typeof(int));
        dt.Columns.Add("Total", typeof(decimal));
        dt.Columns.Add("Percentage", typeof(decimal));

        var orderCounts = new Dictionary<string, int>();
        if (orders != null)
        {
            foreach (var o in orders)
            {
                if (o.OrderDetails == null) continue;
                var catsInOrder = o.OrderDetails
                    .Where(d => d.IsVoided != true)
                    .Select(d => d.CategoryName ?? "غير محدد")
                    .Distinct();
                
                foreach (var c in catsInOrder)
                {
                    if (!orderCounts.ContainsKey(c)) orderCounts[c] = 0;
                    orderCounts[c]++;
                }
            }
        }

        var grouped = items
            .GroupBy(i => i.CategoryName ?? "غير محدد")
            .Select(g => new { Name = g.Key, Total = g.Sum(i => i.TotalAmount) })
            .OrderByDescending(g => g.Total)
            .ToList();

        foreach (var cat in grouped)
        {
            decimal pct = totalSales > 0 ? Math.Round(cat.Total / totalSales * 100, 2) : 0;
            int oCount = orderCounts.ContainsKey(cat.Name) ? orderCounts[cat.Name] : 0;
            dt.Rows.Add(cat.Name, oCount, cat.Total, pct);
        }
        return dt;
    }

    private DataTable ItemSummaryToDataTable(List<SalesItemSummaryDto> itemSummary)
    {
        var dt = new DataTable("ItemSales");
        dt.Columns.Add("ItemId", typeof(int));
        dt.Columns.Add("ItemTitle");
        dt.Columns.Add("Category");
        dt.Columns.Add("Qty", typeof(decimal));
        dt.Columns.Add("Price", typeof(decimal));
        dt.Columns.Add("Total", typeof(decimal));
        dt.Columns.Add("Unit");
        foreach (var i in itemSummary)
        {
            dt.Rows.Add(i.ItemId, i.ItemName, i.CategoryName, i.Quantity, i.UnitPrice, i.TotalAmount, i.Unit);
        }
        return dt;
    }

    private DataTable InventoryToDataTable(IReadOnlyList<InventoryItem> inventory)
    {
        var dt = new DataTable("Inventory");
        dt.Columns.Add("ItemName");
        dt.Columns.Add("Category");
        dt.Columns.Add("CurrentQty", typeof(decimal));
        dt.Columns.Add("MinQty", typeof(decimal));
        dt.Columns.Add("Unit");
        dt.Columns.Add("IsTracked", typeof(bool));
        foreach (var inv in inventory)
        {
            dt.Rows.Add(
                inv.ItemNameAr ?? inv.MenuSalesItem?.ArabicName, 
                inv.CategoryNameAr ?? inv.MenuSalesItem?.Category?.ArabicName, 
                inv.CurrentQuantity, 
                inv.MinimumQuantity,
                inv.UnitNameAr ?? inv.Unit?.ArabicName,
                inv.TrackInventory
            );
        }
        return dt;
    }

    private DataTable TransactionsToDataTable(List<InventoryTransaction> transactions, bool isEnglish = false)
    {
        var dt = new DataTable("Transactions");
        dt.Columns.Add("ItemName");
        dt.Columns.Add("Date", typeof(DateTime));
        dt.Columns.Add("DateOnly", typeof(string));
        dt.Columns.Add("TimeDisplay", typeof(string));
        dt.Columns.Add("Type");
        dt.Columns.Add("QtyChange", typeof(decimal));
        dt.Columns.Add("ResultQty", typeof(decimal));
        dt.Columns.Add("Reference");
        dt.Columns.Add("Reason");
        dt.Columns.Add("CreatedBy");
        dt.Columns.Add("Notes");
        dt.Columns.Add("Image", typeof(byte[]));
        dt.Columns.Add("HasImage", typeof(bool));

        foreach (var t in transactions)
        {
            string typeLabel = isEnglish ? t.Type.ToString() : TranslateTransactionType(t.Type);
            
            byte[]? imgData = null;
            if (t.Images != null && t.Images.Any())
            {
                var firstImg = t.Images.First().Base64Content;
                if (!string.IsNullOrEmpty(firstImg))
                {
                    try
                    {
                        // Some base64 might have prefix data:image/jpeg;base64,
                        if (firstImg.Contains(",")) firstImg = firstImg.Split(',')[1];
                        imgData = Convert.FromBase64String(firstImg);
                    }
                    catch { /* Ignore invalid base64 */ }
                }
            }

            bool hasImage = imgData != null && imgData.Length > 0;
            string amPm = t.CreatedAt.Hour < 12 ? "ص" : "م";
            string dateOnly = t.CreatedAt.ToString("yyyy-MM-dd");
            string timeDisplay = t.CreatedAt.ToString("hh:mm") + " " + amPm;
            dt.Rows.Add(
                t.InventoryItem?.ItemNameAr ?? t.InventoryItem?.MenuSalesItem?.ArabicName ?? "ITEM #" + t.InventoryItemId,
                t.CreatedAt,
                dateOnly,
                timeDisplay,
                typeLabel,
                t.QuantityChange,
                t.ResultingQuantity,
                t.ReferenceId,
                t.Reason,
                t.CreatedBy,
                t.Notes,
                hasImage ? (object)imgData : DBNull.Value,
                hasImage
            );
        }
        return dt;
    }

    private string TranslateTransactionType(TransactionType type)
    {
        return type switch
        {
            TransactionType.StockIn => "توريد مخزني",
            TransactionType.StockOut => "صرف مخزني",
            TransactionType.Sale => "عملية بيع",
            TransactionType.Void => "إلغاء أوردر",
            TransactionType.Adjustment => "تعديل رصيد",
            TransactionType.PhysicalCount => "جرد فعلي",
            TransactionType.Damage => "تالف",
            TransactionType.Waste => "هالك",
            TransactionType.OpeningStock => "رصيد أول المدة",
            _ => type.ToString()
        };
    }

    private DataTable RecipesToDataTable(IReadOnlyList<Recipe> recipes, bool isEnglish = false)
    {
        var dt = new DataTable("Recipes");
        dt.Columns.Add("ProductName");
        dt.Columns.Add("RecipeName");
        dt.Columns.Add("IngredientName");
        dt.Columns.Add("Quantity", typeof(decimal));
        dt.Columns.Add("Unit");

        foreach (var r in recipes)
        {
            string pName = isEnglish ? (r.MenuSalesItem?.EnglishName ?? r.MenuSalesItem?.ArabicName) : r.MenuSalesItem?.ArabicName;
            
            if (r.Ingredients == null || !r.Ingredients.Any())
            {
                dt.Rows.Add(pName, r.RecipeName, isEnglish ? "NO INGREDIENTS" : "لا يوجد مكونات", 0, "-");
                continue;
            }
            foreach (var ing in r.Ingredients)
            {
                string iName = isEnglish ? (ing.MenuSalesIngredient?.EnglishName ?? ing.MenuSalesIngredient?.ArabicName) : ing.MenuSalesIngredient?.ArabicName;
                string uName = isEnglish ? (ing.Unit?.EnglishName ?? ing.Unit?.ArabicName) : ing.Unit?.ArabicName;

                dt.Rows.Add(
                    pName,
                    r.RecipeName,
                    iName,
                    ing.Quantity,
                    uName
                );
            }
        }
        return dt;
    }

    private ReportResponseDto GenerateExcelReport(ReportRequestDto request, DataSet ds)
    {
        using var workbook = new XLWorkbook();
        if (ds == null || ds.Tables.Count == 0)
        {
            var worksheet = workbook.Worksheets.Add("Report");
            worksheet.Cell(1, 1).Value = (request.Language?.ToLower() == "en") ? "No data available" : "لا توجد بيانات متاحة لهذه الفترة";
        }
        else
        {
            foreach (DataTable table in ds.Tables)
            {
                string sheetName = string.IsNullOrWhiteSpace(table.TableName) ? "Sheet1" : table.TableName;
                sheetName = ExcelReportExportService.SanitizeSheetName(sheetName);

                var worksheet = workbook.Worksheets.Add(sheetName);
                if (table.Columns.Count > 0)
                {
                    if (table.Rows.Count > 0)
                    {
                        worksheet.Cell(1, 1).InsertTable(table);
                    }
                    else
                    {
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            worksheet.Cell(1, c + 1).Value = table.Columns[c].ColumnName;
                            worksheet.Cell(1, c + 1).Style.Font.Bold = true;
                        }
                    }
                }
                else
                {
                    worksheet.Cell(1, 1).Value = "No data";
                }
                worksheet.Columns().AdjustToContents();
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ReportResponseDto
        {
            Content = ms.ToArray(),
            FileName = $"{request.ReportId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private async Task<DataTable> SummaryToDataTable(SalesSummaryDto summary, DateTime fromDate, DateTime toDate)
    {
        var dt = new DataTable("Summary");
        dt.Columns.Add("Subtotal", typeof(decimal));
        dt.Columns.Add("DeliveryFees", typeof(decimal));
        dt.Columns.Add("Service", typeof(decimal));
        dt.Columns.Add("Tax", typeof(decimal));
        dt.Columns.Add("MinCharge", typeof(decimal));
        dt.Columns.Add("Reservation", typeof(decimal));
        dt.Columns.Add("OutstandingPay", typeof(decimal));
        dt.Columns.Add("TotalIncome", typeof(decimal));
        dt.Columns.Add("Credit", typeof(decimal));
        dt.Columns.Add("OnTable", typeof(decimal));
        dt.Columns.Add("Outstanding", typeof(decimal));
        dt.Columns.Add("TotalCityLedger", typeof(decimal));
        dt.Columns.Add("Expenses", typeof(decimal));
        dt.Columns.Add("SalesReturn", typeof(decimal));
        dt.Columns.Add("TotalExpenses", typeof(decimal));
        dt.Columns.Add("CashDiscount", typeof(decimal));
        dt.Columns.Add("TotalDiscount", typeof(decimal));
        dt.Columns.Add("NetTotal", typeof(decimal));
        dt.Columns.Add("CashPost", typeof(decimal));
        dt.Columns.Add("Variance", typeof(decimal));
        dt.Columns.Add("NetCash", typeof(decimal));
        dt.Columns.Add("GrossSales", typeof(decimal));
        dt.Columns.Add("OrderCount", typeof(decimal));
        dt.Columns.Add("OrderAVG", typeof(decimal));
        dt.Columns.Add("SoldItemsCount", typeof(decimal));
        dt.Columns.Add("PartialVoidAmount", typeof(decimal));
        dt.Columns.Add("FullVoidAmount", typeof(decimal));
        dt.Columns.Add("DeliveryTotal", typeof(decimal));
        dt.Columns.Add("DeliveryCount", typeof(decimal));
        dt.Columns.Add("DineInTotal", typeof(decimal));
        dt.Columns.Add("DineInCount", typeof(decimal));
        dt.Columns.Add("TakeAwayTotal", typeof(decimal));
        dt.Columns.Add("TakeAwayCount", typeof(decimal));
        
        var subtotal = summary.DineIn.Subtotal + summary.Delivery.Subtotal + summary.TakeAway.Subtotal;
        var deliveryFees = summary.Delivery.DeliveryFees;
        var service = (summary.DineIn.Service + summary.Delivery.Service + summary.TakeAway.Service) - deliveryFees;
        var tax = summary.DineIn.Tax + summary.Delivery.Tax + summary.TakeAway.Tax;
        var totalIncome = summary.Overall.TotalRevenue; 
        var netTotal = summary.Overall.TotalSales; 
        
        var catItems = await _reportingService.GetSalesItemsSummaryAsync(fromDate, toDate);
        var soldItemsCount = catItems.Sum(i => i.Quantity);
        var orderCount = summary.DineIn.OrderCount + summary.Delivery.OrderCount + summary.TakeAway.OrderCount;
        var orderAvg = orderCount > 0 ? netTotal / orderCount : 0;

        dt.Rows.Add(
            subtotal, 
            deliveryFees, 
            service, 
            tax, 
            0, 0, 0, 
            totalIncome, 
            summary.Overall.CreditAmount, 0, summary.Overall.OnAccountAmount, summary.Overall.CreditAmount,
            summary.Overall.Expenses, summary.Overall.RefundAmount, summary.Overall.Expenses, 
            summary.Overall.TotalDiscount, summary.Overall.TotalDiscount, 
            netTotal, 
            summary.Overall.CashAmount, 0, 
            summary.Overall.NetCash,
            netTotal + summary.Overall.TotalDiscount, 
            orderCount,
            orderAvg, 
            soldItemsCount,
            summary.Overall.PartialVoidAmount, summary.Overall.FullVoidAmount,
            summary.Delivery.Total, summary.Delivery.OrderCount,
            summary.DineIn.Total, summary.DineIn.OrderCount,
            summary.TakeAway.Total, summary.TakeAway.OrderCount
        );
        
        return dt;
    }


    private DataTable OrdersToDataTable(List<POS.Contract.Dtos.OrderDtos.OrderDto> orders, string tableName = "Orders")
    {
        var dt = new DataTable(tableName);
        dt.Columns.Add("OrderId");
        dt.Columns.Add("OrderDate", typeof(DateTime));
        dt.Columns.Add("GrandTotal", typeof(decimal));
        dt.Columns.Add("OrderType");
        dt.Columns.Add("OrderState");
        dt.Columns.Add("VoidAmount", typeof(decimal));
        dt.Columns.Add("VoidReason");
        dt.Columns.Add("VoidByName");
        dt.Columns.Add("CashierName");
        dt.Columns.Add("TotalDiscount", typeof(decimal));
        dt.Columns.Add("Date", typeof(DateTime));
        dt.Columns.Add("Total", typeof(decimal));
        dt.Columns.Add("Type");

        foreach (var order in orders)
        {
            dt.Rows.Add(
                order.OrderId, 
                order.OrderDate, 
                order.GrandTotal, 
                order.OrderType!.ToString(), 
                order.OrderState!.ToString(),
                order.VoidAmount ?? 0,
                order.VoidReason,
                order.VoidByName,
                order.CashierName,
                order.TotalDiscount ?? 0,
                order.OrderDate,
                order.GrandTotal,
                order.OrderType.ToString()
            );
        }
        return dt;
    }

    private DataTable HourlySalesToDataTable(List<HourlySalesDto> hourly)
    {
        var dt = new DataTable("HourlySales");
        dt.Columns.Add("HourLabel");
        dt.Columns.Add("Amount", typeof(decimal));
        dt.Columns.Add("OrderCount", typeof(int));
        dt.Columns.Add("DineInAmount", typeof(decimal));
        dt.Columns.Add("DineInCount", typeof(int));
        dt.Columns.Add("TakeAwayAmount", typeof(decimal));
        dt.Columns.Add("TakeAwayCount", typeof(int));
        dt.Columns.Add("DeliveryAmount", typeof(decimal));
        dt.Columns.Add("DeliveryCount", typeof(int));

        foreach (var h in hourly) 
            dt.Rows.Add(h.HourLabel, h.Amount, h.OrderCount, 
                h.DineInAmount, h.DineInCount, 
                h.TakeAwayAmount, h.TakeAwayCount, 
                h.DeliveryAmount, h.DeliveryCount);
        return dt;
    }

    private DataTable ItemsSummaryToDataTable(List<SalesItemSummaryDto> items)
    {
        var dt = new DataTable("ItemsDetails");
        dt.Columns.Add("ItemName");
        dt.Columns.Add("Qty", typeof(decimal));
        dt.Columns.Add("TotalAmount", typeof(decimal));

        foreach (var i in items)
            dt.Rows.Add(i.ItemName, i.Quantity, i.TotalAmount);
            
        return dt;
    }

    private DataTable ModeDetailsToDataTable(List<ModeDetails> modes)
    {
        var dt = new DataTable("ModeDetails");
        dt.Columns.Add("ModeTitle");
        dt.Columns.Add("Subtotal", typeof(decimal));
        dt.Columns.Add("Discount", typeof(decimal));
        dt.Columns.Add("NetSales", typeof(decimal));
        dt.Columns.Add("TotalTaxAndService", typeof(decimal));
        dt.Columns.Add("GrandTotal", typeof(decimal));
        dt.Columns.Add("OrderCount", typeof(int));
        foreach (var m in modes) dt.Rows.Add(m.ModeTitle, m.Subtotal, m.Discount, m.NetSales, m.TotalTaxAndService, m.GrandTotal, m.OrderCount);
        return dt;
    }

    private DataTable VoidEventsToDataTable(List<VoidEventDto> voids, string tableName = "VoidedOrders")
    {
        var dt = new DataTable(tableName);
        dt.Columns.Add("OrderId");
        dt.Columns.Add("OrderDate", typeof(DateTime)); 
        dt.Columns.Add("GrandTotal", typeof(decimal)); 
        dt.Columns.Add("OrderType");
        dt.Columns.Add("VoidAmount", typeof(decimal));
        dt.Columns.Add("VoidReason");
        dt.Columns.Add("VoidByName");
        dt.Columns.Add("IsFullVoid", typeof(bool));
        dt.Columns.Add("GrandTotalAfter", typeof(decimal));

        foreach (var v in voids)
        {
            dt.Rows.Add(
                v.OrderId.ToString(),
                v.VoidDate,
                v.GrandTotalBefore,
                v.OrderType,
                v.TotalVoidedAmount,
                v.Reason,
                v.VoidedByName,
                v.IsFullVoid,
                v.GrandTotalAfter
            );
        }
        return dt;
    }

    private DataTable GetSalesByTypeTable(List<ModeDetails> modes)
    {
        var dt = new DataTable("SalesByType");
        dt.Columns.Add("Type");
        dt.Columns.Add("Count", typeof(int));
        dt.Columns.Add("Void", typeof(decimal));
        dt.Columns.Add("Discount", typeof(decimal));
        dt.Columns.Add("Total", typeof(decimal));
        foreach (var m in modes)
            dt.Rows.Add(m.ModeTitle, m.OrderCount, 0, m.Discount, m.NetSales);
        return dt;
    }

    private DataTable GetEmployeeSalesTable(List<POS.Contract.Dtos.OrderDtos.OrderDto> orders)
    {
        var dt = new DataTable("EmployeeSales");
        dt.Columns.Add("EmployeeName");
        dt.Columns.Add("Discount", typeof(decimal));
        dt.Columns.Add("Void", typeof(decimal));
        dt.Columns.Add("Total", typeof(decimal));
        if (orders != null)
        {
            var groups = orders.GroupBy(o => o.CashierName ?? "System")
                .Select(g => new {
                    Name = g.Key,
                    Discount = g.Sum(o => o.TotalDiscount ?? 0),
                    Void = g.Sum(o => o.VoidAmount ?? (o.OrderState == "Voided" ? o.GrandTotal : 0) ?? 0),
                    Total = g.Sum(o => o.GrandTotal ?? 0)
                });
            foreach (var g in groups) dt.Rows.Add(g.Name, g.Discount, g.Void, g.Total);
        }
        return dt;
    }

    private DataTable GetTopItemsTable(List<SalesItemSummaryDto> items)
    {
        var dt = new DataTable("TopItems");
        dt.Columns.Add("ItemName");
        dt.Columns.Add("Qty", typeof(decimal));
        dt.Columns.Add("Total", typeof(decimal));
        var tops = items.OrderByDescending(i => i.Quantity).Take(10);
        foreach (var i in tops) dt.Rows.Add(i.ItemName, i.Quantity, i.TotalAmount);
        return dt;
    }

    private DataTable GetVoidOrdersTable(List<VoidEventDto> voids)
    {
        var dt = new DataTable("VoidOrders");
        dt.Columns.Add("OrderId");
        dt.Columns.Add("Time");
        dt.Columns.Add("Employee");
        dt.Columns.Add("OrderType");
        dt.Columns.Add("Amount", typeof(decimal));
        foreach (var v in voids)
            dt.Rows.Add(v.OrderId.ToString(), v.VoidDate.ToString("HH:mm"), v.VoidedByName, v.OrderType, v.TotalVoidedAmount);
        return dt;
    }

    private DataTable GetDiscountOrdersTable(List<POS.Contract.Dtos.OrderDtos.OrderDto> orders)
    {
        var dt = new DataTable("DiscountOrders");
        dt.Columns.Add("OrderId");
        dt.Columns.Add("Time");
        dt.Columns.Add("Employee");
        dt.Columns.Add("DiscountType");
        dt.Columns.Add("Amount", typeof(decimal));
        if (orders != null)
        {
            foreach (var o in orders.Where(o => (o.TotalDiscount ?? 0) > 0))
                dt.Rows.Add(o.OrderId, o.OrderDate?.ToString("HH:mm"), o.CashierName, "General", o.TotalDiscount);
        }
        return dt;
    }

    private bool IsThermalRequest(ReportRequestDto request)
    {
        if (request.Filters == null) return false;
        var keysToCheck = new[] { "PrinterType", "PrinterSize", "PageFormat" };
        var valuesToMatch = new[] { "Thermal", "Cashier", "80mm" };
        foreach (var k in keysToCheck)
        {
            if (request.Filters.TryGetValue(k, out var val) && valuesToMatch.Any(v => v.Equals(val, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}
