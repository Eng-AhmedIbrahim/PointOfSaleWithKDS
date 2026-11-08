using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using POS.Core.Repository.Contract;
using AutoMapper;
using POS.Core.Specifications;
using POS.Contract.Dtos.ReportingDtos;
using POS.Core.Entities.OrderEntity;
using POS.Core.Entities.Payment;

namespace POS.Services.ReportingServices;

public class ReportingService : IReportingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReportingService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(DateTime posDate, DateTime? endDate = null)
    {
        var finalEndDate = endDate ?? posDate;
        
        var spec = new BaseSpecifications<Orders>(o => 
            (o.OrderDate.HasValue && o.OrderDate.Value.Date >= posDate.Date.Date && o.OrderDate.Value.Date <= finalEndDate.Date.Date) ||
            (o.VoidTime.HasValue && o.VoidTime.Value.Date >= posDate.Date.Date && o.VoidTime.Value.Date <= finalEndDate.Date.Date));
        
        
        spec.Includes.Add(o => o.OrderDetails!);
        spec.AddThenInclude("OrderDetails.MenuSalesItem.Category");
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);

        var completedOrders = orders.Where(o => o.OrderState == OrderStates.Completed).ToList();
        var pendingOrders = orders.Where(o => o.OrderState != OrderStates.Completed 
        && o.OrderState != OrderStates.Voided && o.OrderState != OrderStates.Canceled).ToList();

        var summary = new SalesSummaryDto { PosDate = posDate };

        summary.DineIn = MapToModeSummary(orders, OrderTypes.DineIn);
        summary.Delivery = MapToModeSummary(orders, OrderTypes.Delivery);
        summary.TakeAway = MapToModeSummary(orders, OrderTypes.TakeAway);
        summary.Staff = MapToModeSummary(orders, OrderTypes.Staff);
        summary.Hospitality = MapToModeSummary(orders, OrderTypes.Hospitality);

        summary.Overall.TotalSales = completedOrders.Sum(o => o.GrandTotal ?? 0);
        
        summary.Overall.CashAmount = completedOrders.Where(o => o.PaymentMethod == PaymentMethod.Cash)
            .Sum(o => (o.GrandTotal ?? 0));
        
        summary.Overall.CreditAmount = completedOrders.Where(o => o.PaymentMethod != PaymentMethod.Cash)
            .Sum(o => (o.GrandTotal ?? 0));
        
        summary.Overall.PendingAmount = pendingOrders.Sum(o => o.GrandTotal ?? 0);
        
        // Use OrderVoid table for accurate void accounting (includes voids of old orders)
        var nextDay = finalEndDate.Date.AddDays(1);
        var voidSpec = new BaseSpecifications<OrderVoid>(v => v.VoidDate >= posDate.Date && v.VoidDate < nextDay);
        voidSpec.Includes.Add(v => v.Order!); // Include Order to get serial OrderID
        var voidsActivity = await _unitOfWork.Repository<OrderVoid>().GetAllWithSpecificationAsync(voidSpec);
        
        summary.Overall.FullVoidAmount = voidsActivity.Where(v => v.IsFullVoid).Sum(v => v.TotalVoidedAmount);
        summary.Overall.PartialVoidAmount = voidsActivity.Where(v => !v.IsFullVoid).Sum(v => v.TotalVoidedAmount);
        summary.Overall.VoidAmount = summary.Overall.FullVoidAmount + summary.Overall.PartialVoidAmount;
        summary.Overall.VoidCount = voidsActivity.Count;

        // Calculate VoidAmount consistently from the Orders table to capture ALL voids (partial and full)
        var ordersWithVoid = orders.Where(o => (o.VoidAmount ?? 0) > 0 || o.OrderState == OrderStates.Voided).ToList();
        var totalVoidFromOrders = ordersWithVoid.Sum(o => o.VoidAmount ?? o.GrandTotal ?? 0);
        
        // But still check if OrderVoid table has more data (e.g., historical voids not easily caught by order state)
        if (totalVoidFromOrders > summary.Overall.VoidAmount)
        {
            summary.Overall.VoidAmount = totalVoidFromOrders;
            summary.Overall.VoidCount = ordersWithVoid.Count;
        }

        summary.VoidEvents = voidsActivity.Select(v => new VoidEventDto
        {
            OrderId = v.Order?.OrderID ?? v.OrderId, // Use serial OrderID if available
            OrderType = v.OrderType.ToString(),
            VoidDate = v.VoidDate,
            VoidedByName = !string.IsNullOrEmpty(v.VoidedByName) ? v.VoidedByName : (v.Order?.VoidByName ?? "System"),
            Reason = v.Reason,
            IsFullVoid = v.IsFullVoid,
            GrandTotalBefore = v.GrandTotalBefore,
            TotalVoidedAmount = v.TotalVoidedAmount,
            GrandTotalAfter = v.GrandTotalAfter
        }).OrderByDescending(v => v.VoidDate).ToList();

        // Fallback for VoidEvents if OrderVoid table is empty or missing data
        if (!summary.VoidEvents.Any() && summary.Overall.VoidAmount > 0)
        {
            summary.VoidEvents = orders.Where(o => (o.VoidAmount ?? 0) > 0 || o.OrderState == OrderStates.Voided)
                .Select(o => new VoidEventDto
                {
                    OrderId = o.OrderID, // Use serial OrderID
                    OrderType = o.OrderType!.ToString() ?? "",
                    VoidDate = o.VoidTime ?? o.OrderDate ?? DateTime.Now,
                    VoidedByName = !string.IsNullOrEmpty(o.VoidByName) ? o.VoidByName : "System",
                    Reason = o.VoidReason ?? "N/A",
                    IsFullVoid = o.OrderState == OrderStates.Voided,
                    TotalVoidedAmount = o.VoidAmount ?? o.GrandTotal ?? 0
                }).OrderByDescending(v => v.VoidDate).ToList();
        }
        
        summary.Overall.TotalDiscount = summary.DineIn.Discount + summary.Delivery.Discount + summary.TakeAway.Discount;
        // Fallback: if ModeSummary didn't capture discount, sum from Orders directly
        if (summary.Overall.TotalDiscount == 0)
        {
            summary.Overall.TotalDiscount = orders.Where(o => o.OrderState == OrderStates.Completed)
                .Sum(o => o.TotalDiscount ?? 0);
        }

        
        // Population of DetailedOrders for report sub-tables
        summary.DetailedOrders = _mapper.Map<List<OrderDto>>(orders.OrderByDescending(o => o.OrderDate).ToList());

        // Analytics: Hourly Sales
        summary.Overall.HourlySales = completedOrders
            .Where(o => o.OrderDate.HasValue)
            .GroupBy(o => o.OrderDate!.Value.Hour)
            .Select(g => new HourlySalesDto 
            { 
                Hour = g.Key, 
                HourLabel = (g.Key == 0) ? "12 صباحاً" : (g.Key == 12) ? "12 مساءً" : (g.Key > 12) ? $"{g.Key - 12} مساءً" : $"{g.Key} صباحاً", 
                Amount = g.Sum(o => o.GrandTotal ?? 0), 
                OrderCount = g.Count(),
                DineInAmount = g.Where(o => o.OrderType == OrderTypes.DineIn).Sum(o => o.GrandTotal ?? 0),
                DineInCount = g.Count(o => o.OrderType == OrderTypes.DineIn),
                TakeAwayAmount = g.Where(o => o.OrderType == OrderTypes.TakeAway).Sum(o => o.GrandTotal ?? 0),
                TakeAwayCount = g.Count(o => o.OrderType == OrderTypes.TakeAway),
                DeliveryAmount = g.Where(o => o.OrderType == OrderTypes.Delivery).Sum(o => o.GrandTotal ?? 0),
                DeliveryCount = g.Count(o => o.OrderType == OrderTypes.Delivery)
            })
            .OrderBy(h => h.Hour)
            .ToList();

        // Analytics: Mode Details (Rich Breakdown)
        var allModes = new List<ModeDetails>
        {
            CreateModeDetail("الصالة", orders, OrderTypes.DineIn),
            CreateModeDetail("التوصيل", orders, OrderTypes.Delivery),
            CreateModeDetail("التيك أواي", orders, OrderTypes.TakeAway),
            CreateModeDetail("العاملين", orders, OrderTypes.Staff),
            CreateModeDetail("ضيافة", orders, OrderTypes.Hospitality)
        };
        summary.Overall.ModeDetails = allModes.Where(m => m.OrderCount > 0 || m.GrandTotal > 0 || m.Discount > 0 || m.VoidAmount > 0).ToList();

        decimal totalValue = summary.Overall.TotalSales + summary.Overall.PendingAmount;
        if (totalValue > 0)
        {
            summary.DineIn.PercentageOfSales = (summary.DineIn.Total + summary.DineIn.UncollectedAmount) / totalValue * 100;
            summary.Delivery.PercentageOfSales = (summary.Delivery.Total + summary.Delivery.UncollectedAmount) / totalValue * 100;
            summary.TakeAway.PercentageOfSales = (summary.TakeAway.Total + summary.TakeAway.UncollectedAmount) / totalValue * 100;
        }

        summary.Overall.Currency = "L.E";

        // 1. Cashier Summaries
        var cashierStats = orders
            .Where(o => !string.IsNullOrEmpty(o.CashierID))
            .GroupBy(o => o.CashierID!)
            .Select(g => new AccountSummaryDto
            {
                Id = g.Key,
                Name = g.FirstOrDefault()?.CashierName ?? "Unknown",
                Type = "Cashier",
                CashAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0),
                CreditAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Visa).Sum(o => o.GrandTotal ?? 0),
                VoidAmount = g.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0))),
                DiscountAmount = g.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0))),
                OrderCount = g.Count(),
                CompletedCount = g.Count(o => o.OrderState == OrderStates.Completed),
                VoidCount = g.Count(o => o.OrderState == OrderStates.Voided || (o.VoidAmount ?? 0) > 0 || (o.TotalVoid ?? 0) > 0),
                DiscountCount = g.Count(o => (o.TotalDiscount ?? 0) > 0 || (o.Discount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0),
                TakeAwayCount = g.Count(o => o.OrderType == OrderTypes.TakeAway),
                DeliveryCount = g.Count(o => o.OrderType == OrderTypes.Delivery),
                DineInCount = g.Count(o => o.OrderType == OrderTypes.DineIn)
            }).ToList();

        // 2. Waiter Summaries (for Dine-In)
        var waiterStats = orders
            .Where(o => !string.IsNullOrEmpty(o.WaiterID))
            .GroupBy(o => o.WaiterID!)
            .Select(g => new AccountSummaryDto
            {
                Id = g.Key,
                Name = g.FirstOrDefault()?.WaiterName ?? "Unknown",
                Type = "Waiter",
                CashAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0),
                CreditAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Visa).Sum(o => o.GrandTotal ?? 0),
                VoidAmount = g.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0))),
                DiscountAmount = g.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0))),
                OrderCount = g.Count(),
                CompletedCount = g.Count(o => o.OrderState == OrderStates.Completed),
                VoidCount = g.Count(o => o.OrderState == OrderStates.Voided || (o.VoidAmount ?? 0) > 0 || (o.TotalVoid ?? 0) > 0),
                DiscountCount = g.Count(o => (o.TotalDiscount ?? 0) > 0 || (o.Discount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0),
                TakeAwayCount = g.Count(o => o.OrderType == OrderTypes.TakeAway),
                DeliveryCount = g.Count(o => o.OrderType == OrderTypes.Delivery),
                DineInCount = g.Count(o => o.OrderType == OrderTypes.DineIn)
            }).ToList();

        // 3. Driver Summaries (for Delivery)
        var driverStats = orders
            .Where(o => !string.IsNullOrEmpty(o.DriverID))
            .GroupBy(o => o.DriverID!)
            .Select(g => new AccountSummaryDto
            {
                Id = g.Key,
                Name = g.FirstOrDefault()?.DriverName ?? "Unknown",
                Type = "Driver",
                CashAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0),
                CreditAmount = g.Where(o => o.OrderState == OrderStates.Completed && o.PaymentMethod == PaymentMethod.Visa).Sum(o => o.GrandTotal ?? 0),
                VoidAmount = g.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0))),
                DiscountAmount = g.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0))),
                OrderCount = g.Count(),
                CompletedCount = g.Count(o => o.OrderState == OrderStates.Completed),
                VoidCount = g.Count(o => o.OrderState == OrderStates.Voided || (o.VoidAmount ?? 0) > 0 || (o.TotalVoid ?? 0) > 0),
                DiscountCount = g.Count(o => (o.TotalDiscount ?? 0) > 0 || (o.Discount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0),
                TakeAwayCount = g.Count(o => o.OrderType == OrderTypes.TakeAway),
                DeliveryCount = g.Count(o => o.OrderType == OrderTypes.Delivery),
                DineInCount = g.Count(o => o.OrderType == OrderTypes.DineIn)
            }).ToList();

        // Combine all and ensure unique entries per ID+Type
        summary.CashierSummaries = cashierStats
            .Concat(waiterStats)
            .Concat(driverStats)
            .GroupBy(s => new { s.Id, s.Type })
            .Select(g => g.First())
            .ToList();

        // 4. Expenses and Payouts
        var expSpec = new BaseSpecifications<POS.Core.Entities.Payment.Expense>(e => e.Date >= posDate.Date && e.Date < nextDay);
        var expenses = await _unitOfWork.Repository<POS.Core.Entities.Payment.Expense>().GetAllWithSpecificationAsync(expSpec);
        summary.DetailedExpenses = expenses.Select(e => new ExpenseDto 
        { 
            Id = e.Id, 
            Date = e.Date, 
            Amount = e.Amount, 
            Description = e.Description, 
            Category = e.Category ?? "General",
            CreatedById = e.CreatedById,
            CreatedByName = e.CreatedByName,
            SpentBy = e.SpentBy,
            IsPayoutFromDrawer = e.IsPayoutFromDrawer,
            BranchId = e.BranchId,
            ShiftId = e.ShiftId
        }).ToList();
        
        summary.Overall.Expenses = summary.DetailedExpenses.Where(e => e.IsPayoutFromDrawer).Sum(e => e.Amount);
        
        // Distribute expense deduction to account summaries if ShiftId is linked
        if (summary.DetailedExpenses.Any())
        {
            foreach(var exp in summary.DetailedExpenses.Where(e => e.IsPayoutFromDrawer && e.ShiftId.HasValue))
            {
                // Attempt to link to cashier summary via shift (assuming account summary matches people who have shifts)
                // In many POS systems, the person who made the payout is tracked.
            }
        }

        return summary;
    }

    private ModeDetails CreateModeDetail(string title, IEnumerable<Orders> orders, OrderTypes type)
    {
        var modeOrders = orders.Where(o => o.OrderType == type).ToList();
        var completed = modeOrders.Where(o => o.OrderState == OrderStates.Completed).ToList();
        
        decimal voidAmount = modeOrders.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0)));
        foreach (var o in completed)
        {
            if (o.OrderDetails != null)
            {
                var itemVoids = o.OrderDetails.Where(i => i.IsVoided == true || (i.TotalVoidAmount ?? 0) > 0 || (i.VoidAmount ?? 0) > 0)
                    .Sum(i => (i.TotalVoidAmount ?? 0) > 0 ? i.TotalVoidAmount.Value : (i.TotalAmount ?? ((i.UnitPrice ?? 0) * (i.Quantity ?? 1))));
                if ((o.VoidAmount ?? 0) == 0 && (o.TotalVoid ?? 0) == 0 && itemVoids > 0)
                {
                    voidAmount += itemVoids;
                }
            }
        }

        return new ModeDetails
        {
            ModeTitle = title,
            Subtotal = completed.Sum(o => o.Subtotal ?? 0),
            Discount = modeOrders.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0))),
            TotalTaxAndService = completed.Sum(o => (o.Tax ?? 0) + (o.Service ?? 0) + (o.DeliveryFees ?? 0)),
            GrandTotal = completed.Sum(o => o.GrandTotal ?? 0),
            OrderCount = modeOrders.Count,
            VoidAmount = voidAmount
        };
    }

    private ModeSummaryDto MapToModeSummary(IEnumerable<Orders> orders, OrderTypes type)
    {
        var modeOrders = orders.Where(o => o.OrderType == type).ToList();
        var completed = modeOrders.Where(o => o.OrderState == OrderStates.Completed).ToList();
        var pending = modeOrders.Where(o => o.OrderState != OrderStates.Completed 
            && o.OrderState != OrderStates.Voided 
            && o.OrderState != OrderStates.Canceled).ToList();

        return new ModeSummaryDto
        {
            Subtotal = completed.Sum(o => (o.Subtotal ?? 0) - (o.Discount ?? 0)),
            Discount = completed.Sum(o => o.Discount ?? 0),
            Tax = completed.Sum(o => o.Tax ?? 0),
            Service =  completed.Sum(o => o.Service ?? 0),
            DeliveryFees = completed.Sum(o => o.DeliveryFees ?? 0),
            Total = completed.Sum(o => o.GrandTotal ?? 0),
            UncollectedAmount = pending.Sum(o => o.GrandTotal ?? 0),
            VoidAmount = modeOrders.Sum(o => o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? o.GrandTotal ?? 0 : 0)),
            VoidCount = modeOrders.Count(o => (o.VoidAmount ?? 0) > 0 || o.OrderState == OrderStates.Voided),
            OrderCount = completed.Count
        };
    }

    public async Task<List<AccountSummaryDto>> GetAccountsSummaryAsync(DateTime posDate, string staffType)
    {
        Expression<Func<Orders, bool>> criteria = o =>
            (o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) ||
            (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date);

        var spec = new BaseSpecifications<Orders>(criteria);
        spec.Includes.Add(o => o.OrderDetails!);
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);

        AccountSummaryDto MapGroup(IGrouping<string, Orders> g, string type)
        {
            var first = g.First();
            string name = type.Equals("Cashier", StringComparison.OrdinalIgnoreCase) ? (first.CashierName ?? "Unknown") :
                          type.Equals("Waiter", StringComparison.OrdinalIgnoreCase) ? (first.WaiterName ?? "Unknown") :
                          (first.DriverName ?? "Unknown");

            var completedOrders = g.Where(o => o.OrderState == OrderStates.Completed).ToList();
            
            decimal cash = completedOrders.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal ?? 0);
            decimal credit = completedOrders.Where(o => o.PaymentMethod == PaymentMethod.Visa).Sum(o => o.GrandTotal ?? 0);
            decimal onAccount = completedOrders.Sum(o => o.Remain ?? 0);

            decimal discount = g.Sum(o => (o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount.Value : ((o.Discount ?? 0) > 0 ? o.Discount.Value : (o.DiscountedItems ?? 0)));

            decimal voidAmount = g.Sum(o => (o.TotalVoid ?? 0) > 0 ? o.TotalVoid.Value : (o.VoidAmount ?? (o.OrderState == OrderStates.Voided ? (o.GrandTotal ?? (o.Subtotal ?? 0)) : 0)));
            foreach (var o in completedOrders)
            {
                if (o.OrderDetails != null)
                {
                    var itemVoids = o.OrderDetails.Where(i => i.IsVoided == true || (i.TotalVoidAmount ?? 0) > 0 || (i.VoidAmount ?? 0) > 0)
                        .Sum(i => (i.TotalVoidAmount ?? 0) > 0 ? i.TotalVoidAmount.Value : (i.TotalAmount ?? ((i.UnitPrice ?? 0) * (i.Quantity ?? 1))));
                    if ((o.VoidAmount ?? 0) == 0 && (o.TotalVoid ?? 0) == 0 && itemVoids > 0)
                    {
                        voidAmount += itemVoids;
                    }
                }
            }

            return new AccountSummaryDto
            {
                Id = g.Key,
                Name = name,
                Type = type,
                CashAmount = cash,
                CreditAmount = credit,
                OnAccountAmount = onAccount,
                DiscountAmount = discount,
                VoidAmount = voidAmount,
                OrderCount = g.Count(),
                CompletedCount = completedOrders.Count,
                VoidCount = g.Count(o => o.OrderState == OrderStates.Voided || (o.VoidAmount ?? 0) > 0 || (o.TotalVoid ?? 0) > 0),
                DiscountCount = g.Count(o => (o.TotalDiscount ?? 0) > 0 || (o.Discount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0),
                TakeAwayCount = g.Count(o => o.OrderType == OrderTypes.TakeAway),
                DeliveryCount = g.Count(o => o.OrderType == OrderTypes.Delivery),
                DineInCount = g.Count(o => o.OrderType == OrderTypes.DineIn)
            };
        }

        if (staffType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var cashiers = orders.Where(o => !string.IsNullOrEmpty(o.CashierID))
                .GroupBy(o => o.CashierID!)
                .Select(g => MapGroup(g, "Cashier"));

            var waiters = orders.Where(o => !string.IsNullOrEmpty(o.WaiterID))
                .GroupBy(o => o.WaiterID!)
                .Select(g => MapGroup(g, "Waiter"));

            var drivers = orders.Where(o => !string.IsNullOrEmpty(o.DriverID))
                .GroupBy(o => o.DriverID!)
                .Select(g => MapGroup(g, "Driver"));

            return cashiers.Concat(waiters).Concat(drivers)
                .GroupBy(s => new { s.Id, s.Type })
                .Select(g => g.First())
                .OrderBy(s => s.Name)
                .ToList();
        }

        IEnumerable<IGrouping<string, Orders>> groups;

        if (staffType.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            groups = orders.Where(o => !string.IsNullOrEmpty(o.CashierID)).GroupBy(o => o.CashierID!);
        else if (staffType.Equals("Waiter", StringComparison.OrdinalIgnoreCase))
            groups = orders.Where(o => !string.IsNullOrEmpty(o.WaiterID)).GroupBy(o => o.WaiterID!);
        else // Driver
            groups = orders.Where(o => !string.IsNullOrEmpty(o.DriverID)).GroupBy(o => o.DriverID!);

        return groups.Select(g => MapGroup(g, staffType)).ToList();
    }

    public async Task<List<OrderDto>> GetTodayOrdersAsync(DateTime posDate, string? orderType = null)
    {
        // Include orders opened today OR orders voided today
        Expression<Func<Orders, bool>> criteria = o => 
            (o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || 
            (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date);

        if (!string.IsNullOrEmpty(orderType) && Enum.TryParse<OrderTypes>(orderType, out var type))
        {
            var original = criteria;
            criteria = o => ((o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || 
                             (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date)) 
                            && o.OrderType == type;
        }

        var spec = new BaseSpecifications<Orders>(criteria);
        spec.Includes.Add(o => o.OrderDetails!);
        spec.AddThenInclude("OrderDetails.OrderItemAttributes");
        spec.AddThenInclude("OrderDetails.MenuSalesItem.Category");
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);
        return _mapper.Map<List<OrderDto>>(orders.OrderByDescending(o => o.OrderDate).ToList());
    }

    public async Task<List<OrderDto>> GetStaffOrdersAsync(DateTime posDate, string staffId, string staffType)
    {
        Expression<Func<Orders, bool>> criteria = o => (o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date);

        bool isAll = staffId.Equals("All", StringComparison.OrdinalIgnoreCase);

        if (staffType.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            criteria = o => ((o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date)) && (isAll ? o.CashierID != null : o.CashierID == staffId);
        else if (staffType.Equals("Waiter", StringComparison.OrdinalIgnoreCase))
            criteria = o => ((o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date)) && (isAll ? o.WaiterID != null : o.WaiterID == staffId);
        else if (staffType.Equals("Driver", StringComparison.OrdinalIgnoreCase))
            criteria = o => ((o.OrderDate.HasValue && o.OrderDate.Value.Date == posDate.Date) || (o.VoidTime.HasValue && o.VoidTime.Value.Date == posDate.Date)) && (isAll ? o.DriverID != null : o.DriverID == staffId);

        var spec = new BaseSpecifications<Orders>(criteria);
        spec.Includes.Add(o => o.OrderDetails!);
        spec.AddThenInclude("OrderDetails.OrderItemAttributes");
        spec.AddThenInclude("OrderDetails.MenuSalesItem.Category");
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);
        return _mapper.Map<List<OrderDto>>(orders.OrderByDescending(o => o.OrderDate).ToList());
    }

    public async Task<List<SalesItemSummaryDto>> GetSalesItemsSummaryAsync(DateTime posDate, DateTime? endDate = null)
    {
        var finalEndDate = endDate ?? posDate;
        // Only include COMPLETED orders to match the financial summary totals
        var spec = new BaseSpecifications<Orders>(
            o => o.OrderDate.HasValue && o.OrderDate.Value.Date >= posDate.Date && o.OrderDate.Value.Date <= finalEndDate.Date && 
                 o.OrderState == OrderStates.Completed);

        spec.Includes.Add(o => o.OrderDetails!);
        spec.AddThenInclude("OrderDetails.OrderItemAttributes");
        spec.AddThenInclude("OrderDetails.MenuSalesItem.Category");
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);

        var menuItems = await _unitOfWork.Repository<MenuSalesItems>().GetAllAsync();
        var categories = await _unitOfWork.Repository<Category>().GetAllAsync();
        var attributeItems = await _unitOfWork.Repository<AttributeItem>().GetAllAsync();
        var menuDict = menuItems.ToDictionary(m => m.Id);
        var catDict = categories.ToDictionary(c => c.Id);
        var attrItemToMenuItemDict = attributeItems.ToDictionary(a => a.Id, a => a.RelatedMenuItemId);

        var rawRows = new List<(int MenuSalesItemId, string ItemName, string CategoryName, decimal Quantity, decimal TotalAmount, decimal UnitPrice, string Unit)>();

        foreach (var order in orders)
        {
            if (order.OrderDetails == null) continue;
            foreach (var d in order.OrderDetails)
            {
                if (d.IsVoided == true) continue;

                var itemName = d.ItemName ?? "";
                var catName = d.CategoryName ?? "";
                var unit = "قطعة";
                MenuSalesItems? menuItem = null;
                if (d.MenuSalesItemId.HasValue && menuDict.TryGetValue(d.MenuSalesItemId.Value, out menuItem))
                {
                    if (string.IsNullOrWhiteSpace(itemName)) itemName = menuItem.ArabicName ?? menuItem.EnglishName ?? "";
                    if (string.IsNullOrWhiteSpace(catName) && menuItem.CategoryId.HasValue && catDict.TryGetValue(menuItem.CategoryId.Value, out var cat))
                        catName = cat.ArabicName ?? cat.EnglishName ?? "";
                    
                    if (menuItem.ByWeight) unit = "كجم";
                }

                if (d.ByWeight == true)
                {
                    unit = "\u0643\u062c\u0645";
                }

                if (string.IsNullOrWhiteSpace(itemName)) itemName = d.ItemNameAr ?? "\u0635\u0646\u0641 \u063a\u064a\u0631 \u0645\u0639\u0631\u0648\u0641";

                // Collect attribute names to append them to the item name
                var attrNames = new List<string>();
                if (d.OrderItemAttributes != null && d.OrderItemAttributes.Any())
                {
                    foreach (var attr in d.OrderItemAttributes)
                    {
                        string aName = attr.AttributeName?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(aName) && attr.AttributeItemId.HasValue && attrItemToMenuItemDict.TryGetValue(attr.AttributeItemId.Value, out var relatedId))
                        {
                            if (menuDict.TryGetValue(relatedId, out var relItem))
                                aName = (relItem.ArabicName ?? relItem.EnglishName ?? "").Trim();
                        }
                        if (!string.IsNullOrWhiteSpace(aName) && !attrNames.Contains(aName, StringComparer.OrdinalIgnoreCase))
                        {
                            attrNames.Add(aName);
                        }
                    }
                }

                var fullItemName = itemName;
                var missingAttrs = attrNames.Where(a => !fullItemName.Contains(a, StringComparison.OrdinalIgnoreCase)).ToList();
                if (missingAttrs.Any())
                {
                    fullItemName = $"{fullItemName} - {string.Join(" - ", missingAttrs)}";
                }

                var lineTotal = d.TotalAmount ?? 0;
                var lineQty = d.Quantity ?? 0;
                decimal unitPrice = lineQty > 0 ? (lineTotal / lineQty) : (menuItem?.Price ?? d.UnitPrice ?? 0);

                rawRows.Add((d.MenuSalesItemId ?? 0, fullItemName, catName, lineQty, lineTotal, unitPrice, unit));
            }
        }

        return rawRows
            .GroupBy(r => new { r.MenuSalesItemId, r.ItemName, r.CategoryName, r.Unit })
            .Select(g => new SalesItemSummaryDto
            {
                ItemId = g.Key.MenuSalesItemId,
                ItemName = string.IsNullOrWhiteSpace(g.Key.ItemName) ? "صنف غير معروف" : g.Key.ItemName,
                CategoryName = string.IsNullOrWhiteSpace(g.Key.CategoryName) ? "بدون تصنيف" : g.Key.CategoryName,
                Quantity = g.Sum(r => r.Quantity),
                TotalAmount = g.Sum(r => r.TotalAmount),
                UnitPrice = g.Sum(r => r.Quantity) > 0 ? (g.Sum(r => r.TotalAmount) / g.Sum(r => r.Quantity)) : 0,
                Unit = g.Key.Unit
            })
            .OrderByDescending(i => i.TotalAmount)
            .ToList();
    }
    public async Task<List<OrderDto>> GetPendingOrdersAsync(DateTime posDate)
    {
        Expression<Func<Orders, bool>> criteria = o => 
            o.OrderDate.HasValue && 
            o.OrderDate.Value.Date == posDate.Date &&
            o.OrderState != OrderStates.Completed &&
            o.OrderState != OrderStates.Voided &&
            o.OrderState != OrderStates.Canceled;

        var spec = new BaseSpecifications<Orders>(criteria);
        spec.Includes.Add(o => o.OrderDetails!);
        var orders = await _unitOfWork.Repository<Orders>().GetAllWithSpecificationAsync(spec);
        return _mapper.Map<List<OrderDto>>(orders.ToList());
    }
}
