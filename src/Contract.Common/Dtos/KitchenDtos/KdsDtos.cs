namespace POS.Contract.Dtos.KitchenDtos;

public class KdsScreenDto
{
    public int Id { get; set; }
    public string ScreenName { get; set; } = string.Empty;
    public string ScreenType { get; set; } = "Kitchen"; // "Kitchen" or "Pickup"
    public string NumberFilter { get; set; } = "All";   // "All", "Odd", "Even"
    public string? SelectedCategoryIds { get; set; }    // Comma-separated (e.g. "1,2,5")
    public string? SelectedItemIds { get; set; }        // Comma-separated
    public bool IsActive { get; set; } = true;
    public int BranchId { get; set; }
    public string? DeviceName { get; set; }
}

public class KdsTicketItemDto
{
    public int Id { get; set; }
    public int? MenuSalesItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemNameAr { get; set; }
    public decimal Quantity { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Comment { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool IsCompleted { get; set; }
    public List<string> Attributes { get; set; } = new();
}

public class KdsOrderTicketDto
{
    public int OrderId { get; set; }
    public int DailyOrderNumber { get; set; }
    public string OrderType { get; set; } = "Takeaway";
    public string OrderTypeName { get; set; } = "تيك أواي";
    public DateTime? OrderDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? TableName { get; set; }
    public string? WaiterName { get; set; }
    public string? OrderNotice { get; set; }
    public DateTime? KitchenOutTime { get; set; }
    public DateTime? PackingOutTime { get; set; }
    public double ElapsedMinutes => OrderDate.HasValue
        ? Math.Max(0, (DateTime.Now - DateTime.Today.Add(OrderDate.Value.TimeOfDay)).TotalMinutes)
        : 0;
    public string Status { get; set; } = "Preparing"; // "Preparing", "Ready", "Delivered"
    public List<KdsTicketItemDto> Items { get; set; } = new();
}

public class KdsStatusUpdateDto
{
    public int OrderId { get; set; }
    public string TargetStatus { get; set; } = "Ready"; // "Ready", "Delivered", "Recall"
}
