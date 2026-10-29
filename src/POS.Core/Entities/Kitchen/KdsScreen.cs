namespace POS.Core.Entities.Kitchen;

public class KdsScreen : BaseEntity
{
    public string ScreenName { get; set; } = string.Empty;
    public string ScreenType { get; set; } = "Kitchen"; // "Kitchen" or "Pickup"
    public string NumberFilter { get; set; } = "All";   // "All", "Odd", "Even"
    public string? SelectedCategoryIds { get; set; }    // Comma-separated Category IDs (e.g. "1,2,5")
    public string? SelectedItemIds { get; set; }        // Comma-separated Item IDs
    public bool IsActive { get; set; } = true;
    public int BranchId { get; set; }
    public string? DeviceName { get; set; }
}
