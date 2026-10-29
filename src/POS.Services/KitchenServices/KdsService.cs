using Microsoft.EntityFrameworkCore;
using POS.Contract.Dtos.KitchenDtos;
using POS.Core.Entities.Kitchen;
using POS.Core.Entities.OrderEntity;
using POS.Core.Services.Contract.KitchenServices;
using Pos.Repository.Data;

namespace POS.Services.KitchenServices;

public class KdsService : IKdsService
{
    private readonly AppDbContext _dbContext;

    public KdsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<KdsScreenDto>> GetScreensAsync(int branchId)
    {
        var screens = await _dbContext.KdsScreens
            .Where(s => branchId <= 0 || s.BranchId == branchId || s.BranchId == 0)
            .OrderBy(s => s.ScreenType)
            .ThenBy(s => s.ScreenName)
            .ToListAsync();

        return screens.Select(MapToDto).ToList();
    }

    public async Task<KdsScreenDto?> GetScreenByIdAsync(int id)
    {
        var screen = await _dbContext.KdsScreens.FindAsync(id);
        return screen != null ? MapToDto(screen) : null;
    }

    public async Task<KdsScreenDto> SaveScreenAsync(KdsScreenDto dto)
    {
        KdsScreen? screen;
        if (dto.Id > 0)
        {
            screen = await _dbContext.KdsScreens.FindAsync(dto.Id);
            if (screen == null)
            {
                screen = new KdsScreen();
                _dbContext.KdsScreens.Add(screen);
            }
        }
        else
        {
            screen = new KdsScreen();
            _dbContext.KdsScreens.Add(screen);
        }

        screen.ScreenName = dto.ScreenName;
        screen.ScreenType = dto.ScreenType ?? "Kitchen";
        screen.NumberFilter = dto.NumberFilter ?? "All";
        screen.SelectedCategoryIds = dto.SelectedCategoryIds;
        screen.SelectedItemIds = dto.SelectedItemIds;
        screen.IsActive = dto.IsActive;
        screen.BranchId = dto.BranchId;
        screen.DeviceName = dto.DeviceName;

        await _dbContext.SaveChangesAsync();
        return MapToDto(screen);
    }

    public async Task<bool> DeleteScreenAsync(int id)
    {
        var screen = await _dbContext.KdsScreens.FindAsync(id);
        if (screen == null) return false;

        _dbContext.KdsScreens.Remove(screen);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<List<KdsOrderTicketDto>> GetActiveKitchenOrdersAsync(int branchId, int screenId)
    {
        // 1. Get Screen config if provided
        KdsScreen? screen = null;
        if (screenId > 0)
        {
            screen = await _dbContext.KdsScreens.FindAsync(screenId);
        }

        var selectedCategoryIds = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(screen?.SelectedCategoryIds))
        {
            foreach (var part in screen.SelectedCategoryIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int catId))
                    selectedCategoryIds.Add(catId);
            }
        }

        var selectedItemIds = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(screen?.SelectedItemIds))
        {
            foreach (var part in screen.SelectedItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int itemId))
                    selectedItemIds.Add(itemId);
            }
        }

        // 2. Query today's active orders (not finished in kitchen, not voided/canceled)
        var today = DateTime.Today;
        var query = _dbContext.Orders
            .Include(o => o.OrderDetails)
            .Where(o => o.OrderDate >= today
                     && o.KitchenOutTime == null
                     && o.ClosingTime == null
                     && o.OrderState != OrderStates.Voided
                     && o.OrderState != OrderStates.Canceled);

        if (branchId > 0)
        {
            query = query.Where(o => o.BranchID == branchId);
        }

        var orders = await query
            .OrderBy(o => o.OrderDate)
            .ToListAsync();

        var result = new List<KdsOrderTicketDto>();

        foreach (var order in orders)
        {
            // Odd / Even check
            if (screen != null && !string.IsNullOrEmpty(screen.NumberFilter) && screen.NumberFilter != "All")
            {
                bool isOdd = (order.OrderID % 2 != 0);
                if (screen.NumberFilter.Equals("Odd", StringComparison.OrdinalIgnoreCase) && !isOdd)
                    continue;
                if (screen.NumberFilter.Equals("Even", StringComparison.OrdinalIgnoreCase) && isOdd)
                    continue;
            }

            // Filter order details
            var activeItems = (order.OrderDetails ?? new List<OrderItemsDetails>())
                .Where(d => d.IsVoided != true)
                .ToList();

            if (selectedCategoryIds.Any())
            {
                activeItems = activeItems
                    .Where(d => d.CategoryId.HasValue && selectedCategoryIds.Contains(d.CategoryId.Value))
                    .ToList();
            }

            if (selectedItemIds.Any())
            {
                activeItems = activeItems
                    .Where(d => d.MenuSalesItemId.HasValue && selectedItemIds.Contains(d.MenuSalesItemId.Value))
                    .ToList();
            }

            // If screen filters categories and this order has no matching items, skip order
            if ((selectedCategoryIds.Any() || selectedItemIds.Any()) && !activeItems.Any())
            {
                continue;
            }

            var ticket = new KdsOrderTicketDto
            {
                OrderId = order.Id,
                DailyOrderNumber = order.OrderID,
                OrderType = order.OrderType?.ToString() ?? "Takeaway",
                OrderTypeName = GetOrderTypeArabic(order.OrderType),
                OrderDate = order.OrderDate,
                CustomerName = order.CustomerName ?? order.TakeawayCustomerName,
                CustomerPhone = order.Phone1 ?? order.TakeawayCustomerPhone,
                TableName = order.TableName,
                WaiterName = order.WaiterName,
                OrderNotice = order.OrderNotice,
                KitchenOutTime = order.KitchenOutTime,
                PackingOutTime = order.PackingOutTime,
                Status = "Preparing",
                Items = activeItems.Select(d => new KdsTicketItemDto
                {
                    Id = d.Id,
                    MenuSalesItemId = d.MenuSalesItemId,
                    ItemName = d.ItemNameAr ?? d.ItemName ?? "ØµÙ†Ù",
                    ItemNameAr = d.ItemNameAr,
                    Quantity = d.Quantity ?? 1,
                    CategoryId = d.CategoryId,
                    CategoryName = d.CategoryName,
                    Comment = d.LineComment,
                    UnitPrice = d.UnitPrice
                }).ToList()
            };

            result.Add(ticket);
        }

        return result;
    }

    public async Task<bool> BumpKitchenOrderAsync(int orderId)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null) return false;

        order.KitchenOutTime = DateTime.Now;
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> RecallKitchenOrderAsync(int orderId)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null) return false;

        order.KitchenOutTime = null;
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<List<KdsOrderTicketDto>> GetActivePickupOrdersAsync(int branchId)
    {
        var today = DateTime.Today;
        var query = _dbContext.Orders
            .Include(o => o.OrderDetails)
            .Where(o => o.OrderDate >= today
                     && o.PackingOutTime == null
                     && o.ClosingTime == null
                     && o.OrderState != OrderStates.Voided
                     && o.OrderState != OrderStates.Canceled);

        if (branchId > 0)
        {
            query = query.Where(o => o.BranchID == branchId);
        }

        var orders = await query
            .OrderBy(o => o.OrderDate)
            .ToListAsync();

        var result = new List<KdsOrderTicketDto>();

        foreach (var order in orders)
        {
            var activeItems = (order.OrderDetails ?? new List<OrderItemsDetails>())
                .Where(d => d.IsVoided != true)
                .Select(d => new KdsTicketItemDto
                {
                    Id = d.Id,
                    MenuSalesItemId = d.MenuSalesItemId,
                    ItemName = d.ItemNameAr ?? d.ItemName ?? "ØµÙ†Ù",
                    ItemNameAr = d.ItemNameAr,
                    Quantity = d.Quantity ?? 1,
                    CategoryId = d.CategoryId,
                    CategoryName = d.CategoryName,
                    Comment = d.LineComment,
                    UnitPrice = d.UnitPrice
                }).ToList();

            bool isReady = order.KitchenOutTime != null;

            result.Add(new KdsOrderTicketDto
            {
                OrderId = order.Id,
                DailyOrderNumber = order.OrderID,
                OrderType = order.OrderType?.ToString() ?? "Takeaway",
                OrderTypeName = GetOrderTypeArabic(order.OrderType),
                OrderDate = order.OrderDate,
                CustomerName = order.CustomerName ?? order.TakeawayCustomerName,
                CustomerPhone = order.Phone1 ?? order.TakeawayCustomerPhone,
                TableName = order.TableName,
                WaiterName = order.WaiterName,
                OrderNotice = order.OrderNotice,
                KitchenOutTime = order.KitchenOutTime,
                PackingOutTime = order.PackingOutTime,
                Status = isReady ? "Ready" : "Preparing",
                Items = activeItems
            });
        }

        return result;
    }

    public async Task<bool> CompletePickupOrderAsync(int orderId)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null) return false;

        order.PackingOutTime = DateTime.Now;
        if (order.OrderState == OrderStates.Pending)
        {
            order.OrderState = OrderStates.Completed;
        }

        return await _dbContext.SaveChangesAsync() > 0;
    }

    private static KdsScreenDto MapToDto(KdsScreen s) => new()
    {
        Id = s.Id,
        ScreenName = s.ScreenName,
        ScreenType = s.ScreenType,
        NumberFilter = s.NumberFilter,
        SelectedCategoryIds = s.SelectedCategoryIds,
        SelectedItemIds = s.SelectedItemIds,
        IsActive = s.IsActive,
        BranchId = s.BranchId,
        DeviceName = s.DeviceName
    };

    private static string GetOrderTypeArabic(OrderTypes? type) => type switch
    {
        OrderTypes.Delivery => "Ø¯Ù„ÙŠÙØ±ÙŠ",
        OrderTypes.DineIn => "ØµØ§Ù„Ø©",
        _ => "ØªÙŠÙƒ Ø£ÙˆØ§ÙŠ"
    };
}
