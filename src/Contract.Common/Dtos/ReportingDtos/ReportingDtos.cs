using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;

namespace POS.Contract.Dtos.ReportingDtos;

public class SalesSummaryDto
{
    public DateTime PosDate { get; set; }
    public string? StaffName { get; set; }
    public string? StaffId { get; set; }
    public List<POS.Contract.Dtos.OrderDtos.OrderDto>? DetailedOrders { get; set; }
    public List<VoidEventDto> VoidEvents { get; set; } = new();
    public List<AccountSummaryDto> CashierSummaries { get; set; } = new();
    public List<ExpenseDto> DetailedExpenses { get; set; } = new();
    public ModeSummaryDto DineIn { get; set; } = new();
    public ModeSummaryDto Delivery { get; set; } = new();
    public ModeSummaryDto TakeAway { get; set; } = new();
    public ModeSummaryDto Staff { get; set; } = new();
    public ModeSummaryDto Hospitality { get; set; } = new();
    public OverallSummaryDto Overall { get; set; } = new();
}

public class VoidEventDto
{
    public int OrderId { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public DateTime VoidDate { get; set; }
    public string VoidedByName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsFullVoid { get; set; }
    public decimal GrandTotalBefore { get; set; }  // قيمة الأوردر قبل الإلغاء
    public decimal TotalVoidedAmount { get; set; } // المبلغ اللي اتلغى فعلاً
    public decimal GrandTotalAfter { get; set; }   // قيمة الأوردر بعد الإلغاء
}

public class ModeSummaryDto
{
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Service { get; set; }
    public decimal DeliveryFees { get; set; }
    public decimal Total { get; set; }
    public decimal UncollectedAmount { get; set; } // Pending/In-progress orders
    public int OrderCount { get; set; }
    public decimal AverageOrder => OrderCount > 0 ? (Total + UncollectedAmount) / OrderCount : 0;
    public decimal VoidAmount { get; set; }
    public decimal VoidCount { get; set; }
    public decimal PercentageOfSales { get; set; }
}

public class OverallSummaryDto
{
    public decimal TotalSales { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal OnAccountAmount { get; set; }
    public decimal PendingAmount { get; set; } // Non-completed orders
    public decimal RefundAmount { get; set; }
    public decimal RefundCount { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal Expenses { get; set; }
    public decimal TotalSalesTax { get; set; }
    public decimal ServiceTotal { get; set; }
    public decimal DineInServiceTotal { get; set; }
    public decimal DeliveryServiceTotal { get; set; }
    public decimal NetCash => CashAmount - Expenses - RefundAmount;
    public decimal TotalRevenue => TotalSales + TotalDiscount;
    public string Currency { get; set; } = "EGP";
    public decimal VoidAmount { get; set; }
    public decimal FullVoidAmount { get; set; }
    public decimal PartialVoidAmount { get; set; }
    public decimal VoidCount { get; set; }
    
    // Analytics
    public List<HourlySalesDto> HourlySales { get; set; } = new();
    public List<ModeDetails> ModeDetails { get; set; } = new();
}

public class ModeDetails
{
    public string ModeTitle { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal NetSales => Subtotal - Discount;
    public decimal TotalTaxAndService { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal VoidAmount { get; set; }
    public int VoidCount { get; set; }
    public int DiscountCount { get; set; }
    public int OrderCount { get; set; }
}

public class HourlySalesDto
{
    public int Hour { get; set; }
    public string HourLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
    
    public decimal DineInAmount { get; set; }
    public int DineInCount { get; set; }
    public decimal TakeAwayAmount { get; set; }
    public int TakeAwayCount { get; set; }
    public decimal DeliveryAmount { get; set; }
    public int DeliveryCount { get; set; }
}

public class AccountSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Cashier, Waiter, Driver
    public decimal CashAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal OnAccountAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal Expenses { get; set; }
    public decimal VoidAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public int OrderCount { get; set; } // Total orders handled
    public int CompletedCount { get; set; }
    public int VoidCount { get; set; }
    public int DiscountCount { get; set; }
    public int TakeAwayCount { get; set; }
    public int DeliveryCount { get; set; }
    public int DineInCount { get; set; }
    public decimal TotalAmount => CashAmount + CreditAmount + OnAccountAmount;
}

public class SalesItemSummaryDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal UnitPrice { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Unit { get; set; } = "القطعة";

    // FastReport compatible names
    public string ItemTitle => ItemName;
    public string Category => CategoryName;
    public decimal Qty => Quantity;
    public decimal Price => UnitPrice;
    public decimal Total => TotalAmount;
}

public class ReportRequestDto
{
    public string ReportId { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, Excel
    public Dictionary<string, string>? Filters { get; set; }
    public string Language { get; set; } = "ar";
}

public class ReportResponseDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

public class InventorySummaryDto
{
    public int Id { get; set; }
    public string ItemNameAr { get; set; } = string.Empty;
    public string ItemNameEn { get; set; } = string.Empty;
    public string CategoryNameAr { get; set; } = string.Empty;
    public string CategoryNameEn { get; set; } = string.Empty;
    public string UnitNameAr { get; set; } = string.Empty;
    public string UnitNameEn { get; set; } = string.Empty;
    public bool TrackInventory { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal MinimumQuantity { get; set; }
}
