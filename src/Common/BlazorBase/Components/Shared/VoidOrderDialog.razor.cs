using BlazorBase.ERPFrontServices.DineInOrderServices;
using BlazorBase.ERPFrontServices.DistributionServices;
using BlazorBase.ERPFrontServices.OrderServices;
using BlazorBase.ERPFrontServices.PrintOrderServices;
using BlazorBase.ERPFrontServices.VoidServices;
using BlazorBase.Models;
using BlazorBase.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using POS.Contract.Dtos.DineIn;
using POS.Contract.Dtos.OrderDtos;
using POS.Contract.Models;
using Serilog;

namespace BlazorBase.Components.Shared;

public partial class VoidOrderDialog
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public int OrderId { get; set; }
    [Parameter] public List<TableItem>? TableItems { get; set; }
    [Parameter] public bool IsDineIn { get; set; } = true;
    [Parameter] public bool IsDistribution { get; set; } = false;
    [Parameter] public OrderDto? InitialDistributionOrder { get; set; }
    [Parameter] public DineInOrderDto? InitialDineInOrder { get; set; }
    
    [Inject] public IDineInOrderFrontService _dineInOrderFrontService { get; set; } = default!;
    [Inject] public IDistributionErpService _distributionErpService { get; set; } = default!;
    [Inject] public IVoidErpService _voidErpService { get; set; } = default!;
    [Inject] public ISnackbar _snackbar { get; set; } = default!;
    [Inject] public LocalizationService _localizer { get; set; } = default!;
    [Inject] public CommonProperties _commonProperties { get; set; } = default!;
    [Inject] public IPrintOrderService _printOrderService { get; set; } = default!;
    [Inject] public IDialogService _dialogService { get; set; } = default!;
    [Inject] public IOrderSettingsService _orderSettingsService { get; set; } = default!;
    
    public OrderDto? DistributionOrder { get; set; }
    public DineInOrderDto? Order { get; set; }
    public List<VoidItemModel> Items { get; set; } = new();
    public VoidItemModel? SelectedItem { get; set; }
    public string NumpadInput { get; set; } = "0";
    public string CustomReason { get; set; } = string.Empty;
    public bool ReturnToStock { get; set; } = true;
    private string Reason => CustomReason;
    
    private decimal OriginalOrderTotal 
    {
        get
        {
            var headerTotal = (DistributionOrder?.GrandTotal ?? 0) + (Order?.GrandTotal ?? 0) + (TableItems?.Sum(i => i.Total ?? 0) ?? 0);
            if (headerTotal == 0 && Items.Any())
                return Items.Sum(i => i.OriginalQuantity * i.UnitPrice);
            return headerTotal;
        }
    }
    private decimal TotalVoidAmount => Items.Sum(i => i.TotalAmount);
    private decimal NewOrderTotal => Math.Max(0, OriginalOrderTotal - TotalVoidAmount);

    protected override async Task OnInitializedAsync()
    {
        if (InitialDistributionOrder != null)
        {
            DistributionOrder = InitialDistributionOrder;
            PopulateFromDistribution();
        }
        else if (InitialDineInOrder != null)
        {
            Order = InitialDineInOrder;
            PopulateFromDineIn();
        }
        else if (OrderId > 0)
        {
            if (IsDineIn)
            {
                Order = await _dineInOrderFrontService.GetDineInOrderByIdAsync(OrderId);
                PopulateFromDineIn();
            }
            else
            {
                var orders = await _distributionErpService.GetUnCompletedDeliveryOrders();
                DistributionOrder = orders?.FirstOrDefault(o => o.Id == OrderId);
                if (DistributionOrder == null && _orderSettingsService != null)
                {
                    DistributionOrder = await _orderSettingsService.GetOrderByIdAsync(OrderId);
                }
                PopulateFromDistribution();
            }
        }
        else if (TableItems != null)
        {
            Items = TableItems
                .Where(x => x.Quantity - (x.VoidAmount ?? 0) > 0)
                .Select(x => new VoidItemModel
            {
                TableItem = x,
                OriginalQuantity = x.Quantity - (x.VoidAmount ?? 0),
                QuantityToVoid = 0,
                UnitPrice = x.Price ?? 0,
                ItemName = _commonProperties?.Language == "ar"
                ? x.NameAr ?? x.Name ?? ""
                : x.Name ?? x.NameAr ?? ""
            }).ToList();
        }

        var orderState = DistributionOrder?.OrderState ?? Order?.OrderState;
        if (orderState == "Dispatched" || orderState == "Delivering")
        {
            _snackbar.Add(_localizer["CannotVoidDispatchedOrder"], Severity.Error);
            MudDialog.Cancel();
        }

        // Initialize ReturnToStock based on feature setting
        ReturnToStock = _commonProperties.IsFeatureEnabled("EnableInventoryTracking");
    }

    private void PopulateFromDistribution()
    {
        if (DistributionOrder?.OrderDetails != null)
        {
            Items = DistributionOrder.OrderDetails
                .Where(x => x.Quantity - (x.VoidAmount ?? 0) > 0)
                .Select(x => new VoidItemModel
                {
                    DistributionItem = x,
                    OriginalQuantity = x.Quantity - (x.VoidAmount ?? 0),
                    QuantityToVoid = 0,
                    UnitPrice = x.Price ?? 0,
                    ItemName = _commonProperties?.Language == "ar" 
                    ? (!string.IsNullOrWhiteSpace(x.NameAr) ? x.NameAr : (!string.IsNullOrWhiteSpace(x.Name) ? x.Name : ""))
                    : (!string.IsNullOrWhiteSpace(x.Name) ? x.Name : (!string.IsNullOrWhiteSpace(x.NameAr) ? x.NameAr : ""))
                }).ToList();
        }
    }

    private void PopulateFromDineIn()
    {
        if (Order?.OrderDetails != null)
        {
            Items = Order.OrderDetails
                .Where(x => (x.Quantity ?? 0) - (x.VoidAmount ?? 0) > 0)
                .Select(x => new VoidItemModel
                {
                    OriginalItem = x,
                    OriginalQuantity = (decimal)((x.Quantity ?? 0) - (x.VoidAmount ?? 0)),
                    QuantityToVoid = 0,
                    UnitPrice = x.Price ?? 0,
                    ItemName = _commonProperties?.Language == "ar" 
                    ? x.ItemNameAr ?? x.ItemName ?? ""
                    : x.ItemName ?? x.ItemNameAr ?? ""
                }).ToList();
        }
    }

    public async Task OnRowClick(TableRowClickEventArgs<VoidItemModel> args)
    {
        SelectedItem = args.Item;
        if (SelectedItem != null)
        {
            NumpadInput = SelectedItem.QuantityToVoid.ToString();
        }
        await Task.CompletedTask;
    }

    private void OnNumpadClick(string key)
    {
        if (SelectedItem == null)
        {
            _snackbar.Add(_localizer["PleaseSelectItemFirst"], Severity.Warning);
            return;
        }

        if (NumpadInput == "0" || NumpadInput == "")
            NumpadInput = key;
        else
            NumpadInput += key;

        UpdateVoidQuantity();
    }

    private void Backspace()
    {
        if (SelectedItem == null) return;

        if (NumpadInput.Length > 1)
            NumpadInput = NumpadInput.Substring(0, NumpadInput.Length - 1);
        else
            NumpadInput = "0";

        UpdateVoidQuantity();
    }

    private void ClearQuantity()
    {
        if (SelectedItem == null) return;
        NumpadInput = "0";
        UpdateVoidQuantity();
    }

    private void UpdateVoidQuantity()
    {
        if (decimal.TryParse(NumpadInput, out decimal qty))
        {
            if (qty > SelectedItem!.OriginalQuantity)
            {
                _snackbar.Add($"{_localizer["CannotVoidMoreThan"]} ({SelectedItem.OriginalQuantity})", Severity.Warning);
                NumpadInput = SelectedItem.OriginalQuantity.ToString();
                SelectedItem.QuantityToVoid = SelectedItem.OriginalQuantity;
            }
            else
                SelectedItem.QuantityToVoid = qty;
        }
        else if (SelectedItem != null)
            SelectedItem.QuantityToVoid = 0;
    }

    private void VoidAll()
    {
        foreach (var item in Items)
            item.QuantityToVoid = item.OriginalQuantity;

        _snackbar.Add(_localizer["AllItemsSelectedForVoid"], Severity.Info);
    }

    private async Task FullOrderVoidConfirm()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, _localizer["ConfirmOperation"] },
            { x => x.ContentText, _localizer["ConfirmEntireOrderVoid"] },
            { x => x.ConfirmText, _localizer["ConfirmVoid"] },
            { x => x.CancelText, _localizer["Cancel"] },
            { x => x.Color, Color.Error },
            { x => x.Icon, Icons.Material.Filled.ReportGmailerrorred },
            { x => x.IconClass, "error" }
        };

        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await _dialogService.ShowAsync<ConfirmDialog>(_localizer["ConfirmOperation"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            VoidAll();
            await Save();
        }
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            _snackbar.Add(_localizer["VoidReasonRequired"], Severity.Error);
            return;
        }

        var itemsToVoid = Items.Where(i => i.QuantityToVoid > 0).ToList();
        if (!itemsToVoid.Any())
        {
            _snackbar.Add(_localizer["NoItemsSelectedToVoid"], Severity.Warning);
            return;
        }

        string voidBy = _commonProperties.CurrentUserId ?? "System";
        string voidByName = _commonProperties.CurrentUser ?? "System";
        bool isFullVoid = itemsToVoid.Count == Items.Count && itemsToVoid.All(i => i.QuantityToVoid == i.OriginalQuantity);

        if (OrderId > 0)
        {
            bool result;
            if (isFullVoid)
            {
                if (DistributionOrder != null) DistributionOrder.OrderState = "Voided";
                if (Order != null) Order.OrderState = "Voided";
                result = await _voidErpService.VoidOrder(OrderId, Reason, voidBy, voidByName, ReturnToStock);
            }
            else
            {
                var voidDtos = itemsToVoid.Select(i =>
                {
                    int detailId = DistributionOrder != null 
                        ? (i.DistributionItem?.DatabaseId > 0 ? i.DistributionItem.DatabaseId : i.DistributionItem?.Id ?? 0)
                        : (i.OriginalItem?.Id ?? 0);
                    return new OrderItemVoidDto(detailId, i.QuantityToVoid);
                }).ToList();

                result = await _voidErpService.VoidItems(OrderId, voidDtos, Reason, voidBy, voidByName, ReturnToStock);
            }

            if (result)
            {
                try
                { 
                    if (IsDineIn && Order != null)
                    {
                        var voidedTableItems = itemsToVoid.Select(i => new TableItem
                        {
                            Id = i.OriginalItem!.MenuSalesItemId ?? 0,
                            DatabaseId = i.OriginalItem.Id,
                            Name = i.OriginalItem.ItemName,
                            NameAr = i.OriginalItem.ItemNameAr,
                            Price = i.OriginalItem.Price,
                            Quantity = i.QuantityToVoid,
                            Total = i.QuantityToVoid * i.OriginalItem.Price,
                            ItemKitchenTypeId = i.OriginalItem.ItemKitchenTypeId,
                            CategoryKitchenTypeId = i.OriginalItem.CategoryKitchenTypeId,
                            CategoryId = i.OriginalItem.CategoryId,
                            CategoryName = i.OriginalItem.CategoryName,
                        }).ToList();

                        await _printOrderService.PrintDineInVoidReceiptAsync(Order, voidedTableItems);
                    }
                    else if (DistributionOrder != null)
                    {
                        var voidedTableItems = itemsToVoid.Select(i =>
                        {
                            var item = i.DistributionItem?.Clone() ?? new TableItem();
                            item.Quantity = i.QuantityToVoid;
                            item.Total = item.Quantity * item.Price;
                            return item;
                        }).ToList();

                        bool isCallCenter = _commonProperties.IsFeatureEnabled("IsCallCenter");
                        if (!isCallCenter)
                        {
                            await _printOrderService.PrintVoidReceiptAsync(DistributionOrder, voidedTableItems);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error printing void receipt for OrderId {OrderId}", OrderId);
                }

                _snackbar.Add(_localizer["VoidOperationSuccessful"], Severity.Success);
                MudDialog.Close(DialogResult.Ok(true));
            }
            else
            {
                _snackbar.Add(_localizer["FailedToVoid"], Severity.Error);
            }
        }
        else
        {
            var voidedItems = itemsToVoid.Select(i => new
            {
                Item = i.TableItem,
                QuantityToVoid = i.QuantityToVoid,
                Reason = Reason
            }).ToList();

            _snackbar.Add(_localizer["VoidOperationSuccessful"], Severity.Success);
            MudDialog.Close(DialogResult.Ok(voidedItems));
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
