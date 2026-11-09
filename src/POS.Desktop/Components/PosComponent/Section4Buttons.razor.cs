namespace POS.Desktop.Components.PosComponent;

using POS.Desktop.Components.PosDialog;
using BlazorBase.Helpers;
using Microsoft.Extensions.Logging;
using BlazorBase.Components.Shared;

public partial class Section4Buttons
{
    [Inject] private ILogger<Section4Buttons> _logger { get; set; } = default!;

    private bool _canCancelOrder;
    private bool _canWaitingOrder;
    private bool _canPrintOrder;

    [Inject] private IPosFeatureSettingsService _featureSettingsService { get; set; } = default!;
    private bool _isProcessing = false;

    private Action? _stateChangedHandler;

    protected override async Task OnInitializedAsync()
    {
        _stateChangedHandler = async () =>
        {
            try
            {
                await InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException) { }
            catch (Exception) { }
        };

        _services.OnChanged += () => _stateChangedHandler?.Invoke();
        _cartService.OnChange += () => _stateChangedHandler?.Invoke();
        _services.OnPrintRequested += OnPrintRequestedHandler;

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity is { IsAuthenticated: true })
        {
            _canCancelOrder  = (await AuthorizationService.AuthorizeAsync(user, "CanCancelOrder")).Succeeded;
            _canWaitingOrder = (await AuthorizationService.AuthorizeAsync(user, "CanWaitingOrder")).Succeeded;
            _canPrintOrder   = (await AuthorizationService.AuthorizeAsync(user, "CanPrintOrder")).Succeeded;
        }
    }

    private async Task CancelOrder()
    {
        try 
        {
            if (_commonProperties.CurrentPosMode == PosModes.DineIn.ToString())
                _cartService.ClearDineInOrderAttributes();
            else
                _cartService.ClearTakeAwayOrderAttributes();

            _commonProperties.ClearStaffMeal();
            _commonProperties.CustomerDetails = new();
            _commonProperties.CustomerName = string.Empty;
            _commonProperties.CustomerPhone = string.Empty;
            _commonProperties.OrderNote = string.Empty;
            _commonProperties.TableItemDisabled = false;
            
            _cartService.CalculateSection4Table();
            
            _commonProperties.CurrentPosMode = "TakeAway";
            _cartService.UpdateFinanceSettingsByMode("TakeAway");
            _services.NotifyStateChanged();

            if (!_navigationManager.Uri.EndsWith("/pos"))
            {
                _navigationManager.NavigateTo("/pos");
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add(Localizer["ErrorCancelingOrder"], Severity.Error);
            _logger.LogError(ex, "CancelOrder fail");
        }
    }

    private Func<Task>? _printHandler;

    private async Task OnPrintRequestedHandler()
    {
        await InvokeAsync(async () =>
        {
            try { await PrintOrder(); } catch { }
        });
    }

    public void Dispose()
    {
        if (_stateChangedHandler != null)
        {
            _services.OnChanged -= () => _stateChangedHandler?.Invoke();
            _cartService.OnChange -= () => _stateChangedHandler?.Invoke();
        }
        _services.OnPrintRequested -= OnPrintRequestedHandler;
    }
    private async Task PrintDeliveryOrder()
    {
        var result = await _printOrderService.PrintDeliveryOrder(0);
        if (result is false)
            return;

        _cartService.ClearTakeAwayOrderAttributes(); // Reuse clearing logic for now
        _commonProperties.CustomerDetails = new();
        _handelDeliveryInvocation.DeliveryDetails = string.Empty;
        _cartService.UpdateFinanceSettingsByMode(_commonProperties.CurrentPosMode);
        _services.NotifyStateChanged();
    }

    private async Task PrintOrder()
    {
        if (_isProcessing) return;

        try
        {
            _isProcessing = true;
            if (_commonProperties!.TableItems!.Any())
            {
                if (_commonProperties.CurrentPosMode == PosModes.TakeAway.ToString())
                {
                    var result = await _printOrderService.PrintTakeAwayOrder(0,
                        _commonProperties.CustomerName ?? "",
                        _commonProperties.CustomerPhone ?? "",
                        _commonProperties.SelectedPaymentMethod);

                    if (result is false)
                        return;

                    _cartService.ClearTakeAwayOrderAttributes();
                    _cartService.UpdateFinanceSettingsByMode(_commonProperties.CurrentPosMode);
                    _services.NotifyStateChanged();
                }

                if (_commonProperties.CurrentPosMode == PosModes.DineIn.ToString())
                {
                    var result = await PrintDineInOrder();
                    if (result is false)
                    {
                        if (_commonProperties.UpdateDineInOrder == true && (_commonProperties.AppendedTableItems?.Any() != true))
                        {
                            _cartService.ClearDineInOrderAttributes();
                        }
                        else return;
                    }
                    else
                    {
                        var existingOrder = _commonProperties.GetActiveOrder();
                        if (existingOrder != null)
                        {
                            // If this is an update (has appended items), print only the new items
                            if (_commonProperties.UpdateDineInOrder && _commonProperties.AppendedTableItems?.Any() == true)
                            {
                                // Create a temporary order with only the appended items for printing
                                var appendedOrder = new DineInOrderDetails
                                {
                                    CaptainId = existingOrder.CaptainId,
                                    CaptainName = existingOrder.CaptainName,
                                    RelatedTableId = existingOrder.RelatedTableId,
                                    RelatedTableName = existingOrder.RelatedTableName,
                                    BasicOrderDetails = new BlazorBase.Models.OrderDetails
                                    {
                                        OrderId = existingOrder.BasicOrderDetails!.OrderId,
                                        CashierName = existingOrder.BasicOrderDetails.CashierName,
                                        Items = _commonProperties.AppendedTableItems.ToList(),
                                        Total = existingOrder.BasicOrderDetails.Total,
                                        Service = existingOrder.BasicOrderDetails.Service,
                                        Tax = existingOrder.BasicOrderDetails.Tax
                                    }
                                };
                                await _printOrderService.PrintInitialDineInOrder(appendedOrder, false, true, isClosing: true, isUpdate: true);
                            }
                            else
                            {
                                // This is a new order, print everything
                                await _printOrderService.PrintInitialDineInOrder(existingOrder, false, true);
                            }
                        }
                    }
                    _cartService.ClearDineInOrderAttributes();
                }

                if (_commonProperties.CurrentPosMode == PosModes.Delivery.ToString())
                {
                    await PrintDeliveryOrder();
                }

                // Determine default mode/url
                if (_commonProperties.FeatureSettings == null || !_commonProperties.FeatureSettings.Any())
                    _commonProperties.FeatureSettings = await _featureSettingsService.GetSettingsByComputerNameAsync(Environment.MachineName);

                string startupUrl = "/pos";
                string startupMode = "TakeAway";

                if (_commonProperties.FeatureSettings != null)
                {
                    if (_commonProperties.FeatureSettings.FirstOrDefault(s => s.FeatureName == "Startup_Delivery")?.Value == true)
                    {
                        startupUrl = "/delivery";
                        startupMode = "Delivery";
                    }
                    else if (_commonProperties.FeatureSettings.FirstOrDefault(s => s.FeatureName == "Startup_DineIn")?.Value == true)
                    {
                        startupUrl = "/dinein";
                        startupMode = "DineIn";
                    }
                    else if (_commonProperties.FeatureSettings.FirstOrDefault(s => s.FeatureName == "Startup_Distribution")?.Value == true)
                    {
                        startupUrl = "/distribution";
                        startupMode = "Distribution";
                    }
                }

                _commonProperties.CurrentPosMode = startupMode;
                _cartService.UpdateFinanceSettingsByMode(_commonProperties.CurrentPosMode);

                // Refresh app date to stay in sync
                var appDate = await _appDateService.GetAppDate();
                if (appDate != null)
                {
                    _commonProperties.PosDate = DateOnly.FromDateTime(appDate.PosDate);
                    _commonProperties.CurrentOrderId = appDate.CurrentOrderNumber + 1;
                }

                _services.NotifyStateChanged();
                
                // If the current screen is NOT the startup screen, navigate back to the startup screen
                if (_navigationManager.Uri.EndsWith("/pos") && startupUrl != "/pos")
                {
                    _navigationManager.NavigateTo(startupUrl);
                }
            }
            else
                _snackbar.Add("No Order to print", Severity.Info);
        }
        catch (Exception ex)
        {
            _snackbar.Add($"{Localizer["ErrorProcessingOrder"]}: {ex.Message}", Severity.Error);
            _logger.LogError(ex, "PrintOrder fail");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    

    private async Task<bool> PrintDineInOrder()
    {
        List<TableItem>? appendedOrder = _commonProperties.AppendedTableItems;
        
        // Check if this is an existing order being updated
        if (_commonProperties.UpdateDineInOrder)
        {
            bool hasNewItems = appendedOrder != null && appendedOrder.Any();
            bool hasDiscountChange = _commonProperties.OrderDiscount?.DiscountType != null;
            
            // If no new items added (only metadata changes like discount, name, phone)
            if (!hasNewItems)
            {
                // Save metadata changes to database without printing
                await UpdateOrderMetadataOnly();
                return false; // Return false to skip printing
            }
            
            // If we have new items, update database and prepare for printing
            if (hasNewItems || hasDiscountChange)
            {
                await UpdateExistingDineInOrderInDatabase(appendedOrder!);
                return true;
            }
        }
        else
        {
            // Brand new order
            if (_commonProperties.TableItems?.Any() == true)
            {
                await CreateNewDineInOrderInDatabase();
                return true;
            }
        }

        return false;
    }

    private async Task UpdateOrderMetadataOnly()
    {
        try
        {
            var currentActiveOrder = _commonProperties.GetActiveOrder();
            if (currentActiveOrder == null) return;

            DineInOrderDetails? existingOrder = CheckIfThereAreDineOrderAndReturnItIfExists();
            if (existingOrder == null) return;

            // Update in-memory order for UI (discount, customer info, etc.)
            AddDineInOrderDiscountIfExists(existingOrder);
            
            // Map and update in database
            var updatedOrder = DineInOrderMapper.MapToDineInOrderDto(
                existingOrder,
                existingOrder.BasicOrderDetails!.OrderId,
                _commonProperties.BranchDetails?.Id ?? 1,
                _commonProperties.BranchDetails?.Name
            );
            
            updatedOrder.Id = existingOrder.DatabaseId;
            updatedOrder.CashierId = _commonProperties.CurrentUserId;
            
            // Update order metadata in database
            var result = await _dineInOrderService.UpdateDineInOrderAsync(updatedOrder);
            
            if (result != null)
            {
                // Update discount if changed
                if (_commonProperties.OrderDiscount?.DiscountType != null)
                {
                    await _dineInOrderService.UpdateDineInOrderDiscountAsync(
                        existingOrder.DatabaseId,
                        _commonProperties.OrderDiscount.Value,
                        _commonProperties.OrderDiscount.Percentage,
                        _commonProperties.OrderDiscount.DiscountType,
                        _commonProperties.OrderDiscount.DiscountReason
                    );
                }
                
                _snackbar.Add("Order updated successfully", Severity.Success);
            }
            else
            {
                _snackbar.Add("Failed to update order", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order metadata");
            _snackbar.Add($"Error updating order: {ex.Message}", Severity.Error);
        }
    }

    private async Task CreateNewDineInOrderInDatabase()
    {
        try
        {
            var orderDetails = FullTableOrderDetails();
            
            // Map to database entity
            var dineInOrder = DineInOrderMapper.MapToDineInOrderDto(
                orderDetails,
                _commonProperties.CurrentOrderId,
                _commonProperties.BranchDetails?.Id ?? 1,
                _commonProperties.BranchDetails?.Name
            );

            dineInOrder.CashierId = _commonProperties.CurrentUserId;
            
            // Save to database
            var createdOrder = await _dineInOrderService.CreateDineInOrderAsync(dineInOrder);
            
            if (createdOrder != null)
            {
                _commonProperties.CurrentOrderId = createdOrder.OrderId + 1;
                _commonProperties.PosDate = DateOnly.FromDateTime(createdOrder.OrderDateTime);
                
                // Update the orderDetails object that will be used for printing
                if (orderDetails.BasicOrderDetails != null)
                {
                    orderDetails.BasicOrderDetails.OrderId = createdOrder.OrderId;
                }

                orderDetails.DatabaseId = createdOrder.Id;
                
                // Update the UI card display
                if (_commonProperties.DineInOrderValues != null)
                {
                    _commonProperties.DineInOrderValues.OrderID = createdOrder.OrderId;
                }
                // Update the in-memory dictionary for UI purposes
                if(!_commonProperties.DineInOrdersDetails!.ContainsKey(_commonProperties.TableId))
                    _commonProperties.DineInOrdersDetails[_commonProperties.TableId] = new List<DineInOrderDetails>();
                
                _commonProperties.DineInOrdersDetails[_commonProperties.TableId].Add(orderDetails);
                
                _snackbar.Add("Order created successfully", Severity.Success);
            }
            else
            {
                _snackbar.Add("Failed to create order", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Error creating order: {ex.Message}", Severity.Error);
        }
    }

    private async Task UpdateExistingDineInOrderInDatabase(List<TableItem> appendedOrder)
    {
        try
        {
            var currentActiveOrder = _commonProperties.GetActiveOrder();
            if (currentActiveOrder == null) return;

            // Get existing order from database using DatabaseId
            // var existingDbOrder = await _dineInOrderService.GetDineInOrderByTableIdAsync(_commonProperties.TableId, "Open");
            // Better to use DatabaseId if available
            int orderDatabaseId = currentActiveOrder.DatabaseId;
            
            // For now assuming existingDbOrder is still needed for mapping OR use currentActiveOrder
            // ... (rest of the logic)
            // Actually, existingDbOrder in the original code was used to get the ID.
            // But we have DatabaseId in currentActiveOrder now (if it was loaded correctly).

            // Let's keep it simple and just use the ID we have.
            
            // حفظ نسخة من الأصناف الجديدة قبل تحديث الأوردر الأساسي
            List<TableItem> newItemsToPrint = appendedOrder!.Select(item => item.Clone()).ToList();
            
            // Update in-memory order for UI
            DineInOrderDetails? existingOrder = CheckIfThereAreDineOrderAndReturnItIfExists();
            UpdateDineInOrderAppendNewItems(appendedOrder!, existingOrder);
            UpdateOrderTotal(existingOrder);
            AddDineInOrderDiscountIfExists(existingOrder);
            
            // Map and update in database
            var updatedOrder = DineInOrderMapper.MapToDineInOrderDto(
                existingOrder,
                existingOrder.BasicOrderDetails!.OrderId,
                _commonProperties.BranchDetails?.Id ?? 1,
                _commonProperties.BranchDetails?.Name
            );
            
            updatedOrder.Id = existingOrder.DatabaseId;
            updatedOrder.CashierId = _commonProperties.CurrentUserId;
            
            // Update order in database
            var result = await _dineInOrderService.UpdateDineInOrderAsync(updatedOrder);
            
            if (result != null)
            {
                // Add new items to database
                if (newItemsToPrint.Any())
                {
                    var newOrderItems = DineInOrderMapper.MapToOrderItemsDetailsDto(newItemsToPrint);
                    await _dineInOrderService.AddItemsToDineInOrderAsync(existingOrder.DatabaseId, newOrderItems);
                }
                
                // Update discount if changed
                if (_commonProperties.OrderDiscount?.DiscountType != null)
                {
                    await _dineInOrderService.UpdateDineInOrderDiscountAsync(
                        existingOrder.DatabaseId,
                        _commonProperties.OrderDiscount.Value,
                        _commonProperties.OrderDiscount.Percentage,
                        _commonProperties.OrderDiscount.DiscountType,
                        _commonProperties.OrderDiscount.DiscountReason
                    );
                }
                
                // تحديث AppendedTableItems لاستخدامها في الطباعة
                appendedOrder!.Clear();
                foreach (var item in newItemsToPrint)
                {
                    appendedOrder.Add(item);
                }
                
                CheckIsTableExist(existingOrder);
                
                // Clear print count in memory as it was reset in DB
                existingOrder.PrintCount = 0;
                
                _snackbar.Add("Order updated successfully", Severity.Success);
            }
            else
            {
                _snackbar.Add("Failed to update order", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order");
            _snackbar.Add($"Error updating order: {ex.Message}", Severity.Error);
        }
    }


    private void UpdateExistingOrderItems(List<TableItem> appendedOrder)
    {
        DineInOrderDetails? existingOrder = CheckIfThereAreDineOrderAndReturnItIfExists();

        UpdateDineInOrderAppendNewItems(appendedOrder, existingOrder);
        UpdateOrderTotal(existingOrder);
        AddDineInOrderDiscountIfExists(existingOrder);
        CheckIsTableExist(existingOrder);
        RemoveItemsIfTableHasItems();
        ClearAppendData();
    }

    private void UpdateOrderTotal(DineInOrderDetails existingOrder)
     => existingOrder.BasicOrderDetails!.Total = _commonProperties.TotalAmountAfterDiscount;

    private void AddDineInOrderDiscountIfExists(DineInOrderDetails existingOrder)
    {
        if (existingOrder!.BasicOrderDetails!.OrderDiscount != _commonProperties.OrderDiscount)
            existingOrder.BasicOrderDetails.OrderDiscount = _commonProperties.OrderDiscount!;
    }

    private DineInOrderDetails CheckIfThereAreDineOrderAndReturnItIfExists()
    {
        var existingOrder = _commonProperties.GetActiveOrder();
        if (existingOrder == null)
        {
            existingOrder = FullTableOrderDetails();
            if(!_commonProperties.DineInOrdersDetails!.ContainsKey(_commonProperties.TableId))
                _commonProperties.DineInOrdersDetails[_commonProperties.TableId] = new List<DineInOrderDetails>();
            
            _commonProperties.DineInOrdersDetails[_commonProperties.TableId].Add(existingOrder);
        }

        return existingOrder;
    }

    private void UpdateDineInOrderAppendNewItems(List<TableItem> appendedOrder, DineInOrderDetails? existingOrder)
    {
        foreach (var newItem in appendedOrder)
        {
            TableItem? existingItem = existingOrder!.BasicOrderDetails!.Items
                .FirstOrDefault(item => (item.Id == newItem.Id || (item.Id == 0 && item.Name == newItem.Name)) && AreAttributesEqual(item, newItem));

            if (existingItem != null)
            {
                existingItem.Quantity += newItem.Quantity;
                existingItem.Total = existingItem.Quantity * existingItem.Price;
                // If the item was partially voided, adding new units makes it active again for those units
                if (existingItem.Quantity > 0)
                {
                    existingItem.IsVoided = false;
                }
            }
            else
                existingOrder.BasicOrderDetails!.Items.Add(newItem);
        }
    }

    private void ClearAppendData()
    {
        _commonProperties.UpdateDineInOrder = false;
        _commonProperties!.AppendedTableItems!.Clear();
    }

    private bool AreAttributesEqual(TableItem item1, TableItem item2)
    {
        var attrs1 = item1.Attributes ?? new List<AttributeDto>();
        var attrs2 = item2.Attributes ?? new List<AttributeDto>();

        if (attrs1.Count != attrs2.Count)
            return false;

        return attrs1.OrderBy(a => a.Id).ThenBy(a => a.Name)
               .SequenceEqual(attrs2.OrderBy(a => a.Id).ThenBy(a => a.Name),
                              new AttributeDtoComparer());
    }

    private void CreateTableOrder()
    {
        var clonedOrder = FullTableOrderDetails();

        if(!_commonProperties.DineInOrdersDetails!.ContainsKey(_commonProperties.TableId))
            _commonProperties.DineInOrdersDetails[_commonProperties.TableId] = new List<DineInOrderDetails>();
        
        _commonProperties.DineInOrdersDetails[_commonProperties.TableId].Add(clonedOrder);

        CheckIsTableExist(clonedOrder);
        RemoveItemsIfTableHasItems();
    }

    private void RemoveItemsIfTableHasItems()
    {
        if (_commonProperties.TableItems?.Any() == true)
            _services.RemoveAllItems(_commonProperties.TableItems);
    }

    private void CheckIsTableExist(DineInOrderDetails clonedOrder)
    {
        if (clonedOrder.RelatedTableId != _commonProperties.TableId &&
                    !_commonProperties!.DineInOrdersDetails!.ContainsKey(clonedOrder.RelatedTableId ?? 0))
        {
            _commonProperties!.DineInOrdersDetails!.Add(clonedOrder.RelatedTableId ?? 0, new List<DineInOrderDetails> { clonedOrder });
        }
    }

    private DineInOrderDetails FullTableOrderDetails()
    {

        var clonedOrder = new DineInOrderDetails
        {
            CaptainId = _commonProperties!.CurrentDineInOrder!.CaptainId,
            CaptainName = _commonProperties.CurrentDineInOrder.CaptainName,
            RelatedTableId = _commonProperties.CurrentDineInOrder.RelatedTableId,
            RelatedTableName = _commonProperties.CurrentDineInOrder.RelatedTableName,
            BasicOrderDetails = new BlazorBase.Models.OrderDetails
            {
                OrderId = _commonProperties.CurrentOrderId,
                CashierName = _commonProperties.CurrentUser,
                OrderType = OrderTypes.DineIn.ToString(),
                OrderDataTime = _commonProperties.PosDate.HasValue
                    ? _commonProperties.PosDate.Value.ToDateTime(TimeOnly.FromTimeSpan(DateTime.Now.TimeOfDay))
                    : DateTime.Now,
                Items = _commonProperties.TableItems?.Select(item => item.Clone()).ToList() ?? new List<TableItem>(),
                Account = _commonProperties._financeSettingsList![0].Value ?? 0M,
                Total = _commonProperties.TotalAmountAfterDiscount,
                Service = _commonProperties!.CurrentDineInOrder!.BasicOrderDetails!.Service,
                Tax = _commonProperties.CurrentDineInOrder.BasicOrderDetails.Tax,
                OrderDiscount = _commonProperties!.OrderDiscount!,
                CustomerName = _commonProperties.CustomerName,
                CustomerPhone = _commonProperties.CustomerPhone,
                PaymentMethod = _commonProperties.SelectedPaymentMethod,
                MachineName = Environment.MachineName
            }
        };

        return clonedOrder;

    }

    private void AddOrderToWaitingQueue()
    {
        if (_commonProperties.CurrentPosMode == PosModes.TakeAway.ToString())
            _services.AddOrderToWaitingQueue(_commonProperties?.TableItems ?? []);
        else
            _snackbar.Add("Only TakeAway Orders Can Be Added To Waiting Queue", Severity.Info);
    }

    
    private async Task OpenVoidDialog()
    {
        if (_commonProperties.TableItems == null || !_commonProperties.TableItems.Any())
        {
            _snackbar.Add(Localizer["NoItemsToVoid"], Severity.Info);
            return;
        }

        var parameters = new DialogParameters 
        { 
            ["TableItems"] = _commonProperties.TableItems,
            ["IsDineIn"] = false 
        };
        var options = new DialogOptions { CloseButton = false, MaxWidth = MaxWidth.Large, FullWidth = true };
        
        var dialog = await _dialogService.ShowAsync<VoidOrderDialog>(Localizer["VoidOrder"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data != null)
        {
            if (_commonProperties.VoidedTableItems == null)
                _commonProperties.VoidedTableItems = new List<TableItem>();

            // Handle voided items for TakeAway/Delivery
            var voidedItems = result.Data as IEnumerable<dynamic>;
            if (voidedItems != null)
            {
                foreach (var voidedItem in voidedItems)
                {
                    // Using reflection or dynamic access to get properties
                    object itemObj = voidedItem.GetType().GetProperty("Item")?.GetValue(voidedItem, null);
                    var item = itemObj as TableItem;
                    
                    decimal qtyToVoid = 0m;
                    var qtyProp = voidedItem.GetType().GetProperty("QuantityToVoid");
                    if (qtyProp != null)
                    {
                        var rawVal = qtyProp.GetValue(voidedItem, null);
                        if (rawVal != null)
                            qtyToVoid = Convert.ToDecimal(rawVal);
                    }
                        
                    string reason = "";
                    var reasonProp = voidedItem.GetType().GetProperty("Reason");
                    if (reasonProp != null)
                        reason = (string)reasonProp.GetValue(voidedItem, null) ?? "";

                    if (item != null && qtyToVoid > 0)
                    {
                        // Create a record of the voided item
                        var voidedRecord = item.Clone();
                        voidedRecord.Quantity = qtyToVoid; // This is the voided quantity
                        voidedRecord.IsVoided = true;
                        voidedRecord.VoidAmount = qtyToVoid;
                        voidedRecord.VoidReason = reason;
                        voidedRecord.VoidTime = DateTime.Now;
                        voidedRecord.VoidBy = _commonProperties.CurrentUserId;
                        voidedRecord.VoidByName = _commonProperties.CurrentUser;
                        voidedRecord.Total = (voidedRecord.Price ?? 0m) * qtyToVoid;
                        voidedRecord.TotalAmount = voidedRecord.Total;

                        _commonProperties.VoidedTableItems.Add(voidedRecord);

                        // Find the item in the current order
                        var orderItem = _commonProperties.TableItems.FirstOrDefault(x => x == item || (x.Id == item.Id && x.DatabaseId == item.DatabaseId));
                        if (orderItem != null)
                        {
                            if (qtyToVoid >= orderItem.Quantity)
                            {
                                // Remove item completely
                                _commonProperties.TableItems.Remove(orderItem);
                            }
                            else
                            {
                                // Reduce quantity
                                orderItem.Quantity -= qtyToVoid;
                                orderItem.Total = orderItem.Quantity * (orderItem.Price ?? 0m);
                                orderItem.TotalAmount = orderItem.Total - (orderItem.TotalDiscountPrice ?? 0);
                            }
                        }
                    }
                }
                
                // Recalculate totals
                _cartService.UpdateFinanceSettingsByMode(_commonProperties.CurrentPosMode);
                _services.NotifyStateChanged();
                StateHasChanged();
            }
        }
    }

    private async Task ReprintOrder()
    {
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await _dialogService.ShowAsync<ReprintOrderDialog>(Localizer["Reprint Order"], options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is int orderId && orderId > 0)
        {
            _isProcessing = true;
            try
            {
                var success = await _printOrderService.ReprintOrderAsync(orderId);
                if (success)
                    _snackbar.Add(Localizer["Order reprinted successfully"], Severity.Success);
                else
                    _snackbar.Add(Localizer["Order not found or print failed"], Severity.Error);
            }
            catch (Exception ex)
            {
                _snackbar.Add("Error reprinting order", Severity.Error);
                _logger.LogError(ex, "Reprint fail");
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}