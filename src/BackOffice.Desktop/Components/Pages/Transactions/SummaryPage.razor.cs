using BackOffice.Desktop.Components.Pages.Reporting;

namespace BackOffice.Desktop.Components.Pages.Transactions;

public partial class SummaryPage
{
    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IPrintOrderService PrintOrderService { get; set; } = default!;
    [Inject] private IBranchService BranchService { get; set; } = default!;


    [SupplyParameterFromQuery]
    [Parameter]
    public string? Start { get; set; }

    [SupplyParameterFromQuery]
    [Parameter]
    public string? End { get; set; }

    private bool _canViewDetailedSales;
    private bool _canViewSalesItems;
    private bool _canPrint;
    private SalesSummaryDto _summaryData = new();
    private List<SalesItemSummaryDto> _salesItems = new();
    private bool _isLoading = true;
    private DateRange _dateRange = new DateRange(null, null);

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity is { IsAuthenticated: true })
        {
            _canViewDetailedSales = (await AuthorizationService.AuthorizeAsync(user, "CanViewSummaryDetailsAtBackOffice")).Succeeded;
            _canViewSalesItems = (await AuthorizationService.AuthorizeAsync(user, "CanViewSalesItemsAtBackOffice")).Succeeded;
            _canPrint = (await AuthorizationService.AuthorizeAsync(user, "CanPrintSummaryReportAtBackOffice")).Succeeded;
        }

        try
        {
            if (_commonProperties.BranchDetails?.Id == null || _commonProperties.BranchDetails.Id == 0)
            {
                var branches = await BranchService.GetBranches();
                if (branches != null && branches.Any())
                {
                    _commonProperties.BranchDetails = branches.FirstOrDefault();
                }
            }

            var appDate = await AppDateService.GetAppDate();
            var dtStart = appDate != null && appDate.PosDate != default ? appDate.PosDate : DateTime.Today;
            var dtEnd = dtStart;

            if (DateTime.TryParse(Start, out var pStart) && DateTime.TryParse(End, out var pEnd))
            {
                dtStart = pStart;
                dtEnd = pEnd;
            }

            _dateRange = new MudBlazor.DateRange(dtStart, dtEnd);
            await LoadSummaryData();
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error loading summary: " + ex.Message, Severity.Error);
            _isLoading = false;
        }
    }

    private async Task OnDateRangeChanged(MudBlazor.DateRange range)
    {
        _dateRange = range;
        if (_dateRange.Start.HasValue)
        {
            await LoadSummaryData();
        }
    }

    private async Task LoadSummaryData()
    {
        _isLoading = true;
        StateHasChanged();
        try
        {
            DateTime startDate;
            DateTime endDate;

            if (_dateRange.Start.HasValue)
            {
                startDate = _dateRange.Start.Value;
                endDate = _dateRange.End ?? startDate;
            }
            else
            {
                var appDate = await AppDateService.GetAppDate();
                startDate = appDate != null && appDate.PosDate != default ? appDate.PosDate : DateTime.Today;
                endDate = startDate;
                _dateRange = new DateRange(startDate, endDate);
            }

            _summaryData = await ReportingService.GetSalesSummary(startDate, endDate);
            _salesItems = await ReportingService.GetSalesItemsSummary(startDate, endDate);
            if (string.IsNullOrEmpty(_summaryData.Overall.Currency))
                _summaryData.Overall.Currency = "EGP";
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error loading summary: " + ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void GoBack()
    {
        System.Environment.Exit(0);
    }

    private async Task ShowSalesItems()
    {
        try
        {
            var startDate = _dateRange.Start ?? DateTime.Today;
            var endDate = _dateRange.End ?? startDate;
            var items = await ReportingService.GetSalesItemsSummary(startDate, endDate);
            var parameters = new DialogParameters { ["Items"] = items, ["SelectedDate"] = startDate };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = false, NoHeader = false };
            await DialogService.ShowAsync<global::BackOffice.Desktop.Components.Pages.Transactions.SummaryPages.SalesItemsDialog>("", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error loading sales items: " + ex.Message, Severity.Error);
        }
    }

    private async Task ShowDetailedSales()
    {
        try
        {
            var startDate = _dateRange.Start ?? DateTime.Today;
            var parameters = new DialogParameters { ["SelectedDate"] = startDate };
            var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true };
            await DialogService.ShowAsync<global::BackOffice.Desktop.Components.Pages.Transactions.SummaryPages.DetailedSalesDialog>("", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error loading detailed sales: " + ex.Message, Severity.Error);
        }
    }

    private async Task PreviewFastReport(string reportId, string reportName)
    {
        var startDate = _dateRange.Start ?? DateTime.Today;
        var endDate = _dateRange.End ?? startDate;
        
        var parameters = new DialogParameters<ReportViewerDialog>
        {
            { x => x.ReportId, reportId },
            { x => x.ReportName, reportName },
            { x => x.FromDate, startDate },
            { x => x.ToDate, endDate }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Large, FullWidth = true, FullScreen = true };
        await DialogService.ShowAsync<ReportViewerDialog>(reportName, parameters, options);
    }

    private async Task PrintEndDayReport()
    {
        try
        {
            var startDate = _dateRange.Start ?? DateTime.Today;
            var endDate = _dateRange.End ?? startDate;
            var items = await ReportingService.GetSalesItemsSummary(startDate, endDate);
            await PrintOrderService.PrintEndDayReportAsync(_summaryData, items, null, false, true);
            Snackbar.Add("End of day report sent to printer", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Error printing report: " + ex.Message, Severity.Error);
        }
    }
}
