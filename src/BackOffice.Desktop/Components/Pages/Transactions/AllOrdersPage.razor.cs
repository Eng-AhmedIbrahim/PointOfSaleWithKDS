// using BackOffice.Desktop.Components.Pages.Transactions; // Moved to same namespace
using BlazorBase.Components.Shared;

namespace BackOffice.Desktop.Components.Pages.Transactions;

public partial class AllOrdersPage
{
    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IPrintOrderService PrintOrderService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<OrderDto> Orders = new();
    private bool _isLoading = false;
    private int _activeTab = 0;
    private string _searchText = "";
    private DateTime SelectedDate = DateTime.Today;

    private bool _canViewOrder;
    private bool _canPrintCustomerReceipt;
    private bool _canPrintKitchenReceipt;
    private bool _canVoidOrder;

    private IEnumerable<OrderDto> FilteredOrders {
        get {
            var filtered = Orders.AsEnumerable();
            
            // Tab Filter
            if (_activeTab == 1) filtered = filtered.Where(o => o.OrderType == "DineIn");
            else if (_activeTab == 2) filtered = filtered.Where(o => o.OrderType == "Delivery");
            else if (_activeTab == 3) filtered = filtered.Where(o => o.OrderType == "TakeAway");

            // Search Filter
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var s = _searchText.ToLower();
                filtered = filtered.Where(o => 
                    (o.OrderId.ToString().Contains(s)) ||
                    (o.CustomerName?.ToLower().Contains(s) ?? false) ||
                    (o.Phone1?.Contains(s) ?? false)
                );
            }

            return filtered;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var appDate = await AppDateService.GetAppDate();
        SelectedDate = appDate.PosDate;

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity is { IsAuthenticated: true })
        {
            _canViewOrder            = (await AuthorizationService.AuthorizeAsync(user, "CanViewOrderAtBackOffice")).Succeeded;
            _canPrintCustomerReceipt = (await AuthorizationService.AuthorizeAsync(user, "CanPrintOrderAtBackOffice")).Succeeded;
            _canPrintKitchenReceipt  = (await AuthorizationService.AuthorizeAsync(user, "CanPrintKitchenOrderAtBackOffice")).Succeeded;
            _canVoidOrder            = (await AuthorizationService.AuthorizeAsync(user, "CanVoidOrderAtBackOffice")).Succeeded;
        }

        await LoadOrders();
    }

    private async Task LoadOrders()
    {
        _isLoading = true;
        try
        {
            Orders = await ReportingService.GetTodayOrders(SelectedDate);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error: " + ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Color GetOrderTypeColor(string? type) => type switch
    {
        "DineIn" => Color.Info,
        "Delivery" => Color.Warning,
        "TakeAway" => Color.Success,
        _ => Color.Default
    };

    private Color GetStatusColor(string? status) => status switch
    {
        "Completed" => Color.Success,
        "Voided" => Color.Error,
        "Pending" => Color.Primary,
        "Dispatched" => Color.Warning,
        _ => Color.Default
    };

    private void GoBack()
    {
        _navigationManager.NavigateTo("/");
    }

    private async Task ViewOrder(OrderDto order)
    {
        var parameters = new DialogParameters { ["Order"] = order };
        var options = new DialogOptions { CloseButton = false, MaxWidth = MaxWidth.Large, FullWidth = true };
        await DialogService.ShowAsync<DeliveryOrderViewDialog>(Localizer["OrderDetails"], parameters, options);
    }

    private async Task PrintCustomerOrderReceipt(OrderDto order)
    {
        try
        {
            var parameters = new DialogParameters { ["Order"] = order, ["PreviewMode"] = "Customer" };
            var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
            await DialogService.ShowAsync<global::BackOffice.Desktop.Components.Pages.Transactions.OrderReceiptPreviewDialog>("", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error printing: " + ex.Message, Severity.Error);
        }
    }

    private async Task PrintBackupOrderReceipt(OrderDto order)
    {
        try
        {
            var parameters = new DialogParameters { ["Order"] = order, ["PreviewMode"] = "Kitchen" };
            var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
            await DialogService.ShowAsync<global::BackOffice.Desktop.Components.Pages.Transactions.OrderReceiptPreviewDialog>("", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error printing: " + ex.Message, Severity.Error);
        }
    }

    private async Task VoidOrder(OrderDto order)
    {
        if (order.OrderState == "Dispatched")
        {
            Snackbar.Add(Localizer["CannotVoidDispatchedOrder"], Severity.Warning);
            return;
        }

        bool isDineIn = order.OrderType == "DineIn";
        var parameters = new DialogParameters
        {
            ["OrderId"] = order.Id,
            ["IsDineIn"] = isDineIn,
            ["IsDistribution"] = !isDineIn,
            ["InitialDistributionOrder"] = order
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = false };
        var dialog = await DialogService.ShowAsync<VoidOrderDialog>(Localizer["VoidOrder"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            await LoadOrders();
        }
    }
}
