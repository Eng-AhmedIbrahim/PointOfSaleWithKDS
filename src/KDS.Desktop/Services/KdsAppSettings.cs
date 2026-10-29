namespace KDS.Desktop.Services;

/// <summary>
/// KDS configuration loaded from appsettings.json → KdsSettings section.
/// ScreenType: "Kitchen" or "Pickup"
/// BranchId: 0 = all branches
/// ScreenId: the DB row id of the KdsScreen
/// </summary>
public class KdsAppSettings
{
    public int BranchId { get; set; } = 1;
    public int ScreenId { get; set; } = 1;

    /// <summary>"Kitchen" or "Pickup"</summary>
    public string ScreenType { get; set; } = "Kitchen";

    public bool IsKitchen => ScreenType?.Equals("Kitchen", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsPickup  => ScreenType?.Equals("Pickup",  StringComparison.OrdinalIgnoreCase) == true;
}
