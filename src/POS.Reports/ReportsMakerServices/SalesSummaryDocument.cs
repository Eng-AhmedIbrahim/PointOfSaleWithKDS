namespace POS.Reports.ReportsMakerServices;

public class SalesSummaryDocument : IDocument
{
    private readonly SalesSummaryDto _summary;
    private readonly List<SalesItemSummaryDto> _catItems;
    private readonly string _branchName;
    private readonly string _logoPath;
    private readonly ReportPageFormat _format;
    private readonly bool _isArabic;

    public SalesSummaryDocument(SalesSummaryDto summary,
        List<SalesItemSummaryDto> catItems, 
        string branchName, 
        string logoPath, 
        ReportPageFormat format, 
        bool isArabic = true)
    {
        _summary = summary;
        _catItems = catItems;
        _branchName = branchName;
        _logoPath = logoPath;
        _format = format;
        _isArabic = isArabic;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        
        var voidOrders = (_summary.DetailedOrders ?? new List<Contract.Dtos.OrderDtos.OrderDto>())
                         .Where(o => (o.VoidAmount ?? 0) > 0).ToList();

        var vEvents = _summary.VoidEvents ?? new List<VoidEventDto>();

        var discountOrders = (_summary.DetailedOrders ?? new List<Contract.Dtos.OrderDtos.OrderDto>())
                             .Where(o => (o.TotalDiscount ?? 0) > 0 || (o.TotalOrderDiscount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0).ToList();

        container.Page(page =>
        {
            if (_format == ReportPageFormat.Cashier)
            {
                page.ContinuousSize(8f, Unit.Centimetre);
                page.Margin(2, Unit.Millimetre);
            }
            else
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
            }

            page.PageColor(Colors.White);
            
            var baseStyle = 
            TextStyle.Default
            .FontFamily("Arial", "Noto Sans Arabic")
            .FontSize(_format == ReportPageFormat.Cashier ? 9 : 11);
            
            page.DefaultTextStyle(baseStyle);

            if (_isArabic)
                page.ContentFromRightToLeft();

            var contentFontSize = _format == ReportPageFormat.Cashier ? 8 : 10;
            var headerFontSize = _format == ReportPageFormat.Cashier ? 9 : 12;

            page.Content().Column(col =>
            {
                col.Spacing(2);
                
                // 1. Title
                col.Item().AlignCenter().Text(_isArabic ? "ملخص المبيعات" : "Sales Summary").FontSize(14).Bold();
                
                // 2. Branch Info Table (Match First Screenshot)
                col.Item().PaddingTop(5).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(3); });
                    t.Cell().Border(1).Padding(2).AlignCenter().Text(_isArabic ? "اسم الفرع" : "Branch").Bold();
                    t.Cell().Border(1).Padding(2).AlignCenter().Text(_branchName);
                    t.Cell().Border(1).Padding(2).AlignCenter().Text(_isArabic ? "التاريخ" : "Date").Bold();
                    t.Cell().Border(1).Padding(2).AlignCenter().Text(_summary.PosDate.ToString("yyyy-MM-dd"));
                });

                // 3. Net Revenue Section (Dark Header)
                ComposeSectionHeader(col, _isArabic ? "صافي الإيراد" : "Net Revenue", headerFontSize);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); });
                    AddFinancialRow(t, _isArabic ? "صافي المبيعات" : "Net Sales", _summary.Overall.TotalSales.ToString("0.##"));
                    AddFinancialRow(t, _isArabic ? "نقدي" : "Cash", _summary.Overall.CashAmount.ToString("0.##"));
                    AddFinancialRow(t, _isArabic ? "فيزا / بطاقة" : "Visa / Card", _summary.Overall.CreditAmount.ToString("0.##"));
                });

                // 4. Sales Distribution
                ComposeSectionHeader(col, _isArabic ? "توزيع المبيعات" : "Sales Distribution", headerFontSize);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { 
                        cd.RelativeColumn(2); // النوع
                        cd.RelativeColumn(1.2f); // العدد
                        cd.RelativeColumn(1.8f); // الخصم
                        cd.RelativeColumn(1.8f); // الفويد
                        cd.RelativeColumn(2); // الإجمالي
                    });
                    t.Header(h =>
                    {
                        h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "النوع" : "Type").Bold().FontSize(contentFontSize);
                        h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "العدد" : "Qty").Bold().FontSize(contentFontSize);
                        h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الخصم" : "Discount").Bold().FontSize(contentFontSize);
                        h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الفويد" : "Void").Bold().FontSize(contentFontSize);
                        h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الإجمالي" : "Total").Bold().FontSize(contentFontSize);
                    });

                    foreach (var m in _summary.Overall.ModeDetails)
                    {
                        var voidAmount = 0m;
                        var vList = _summary.VoidEvents ?? new List<VoidEventDto>();
                        if (m.ModeTitle.Contains("صالة") || m.ModeTitle.Contains("Dine"))
                            voidAmount = vList.Where(v => v.OrderType.Equals("DineIn", StringComparison.OrdinalIgnoreCase)).Sum(v => v.TotalVoidedAmount);
                        else if (m.ModeTitle.Contains("تيك") || m.ModeTitle.Contains("Take"))
                            voidAmount = vList.Where(v => v.OrderType.Equals("TakeAway", StringComparison.OrdinalIgnoreCase)).Sum(v => v.TotalVoidedAmount);
                        else if (m.ModeTitle.Contains("توصيل") || m.ModeTitle.Contains("Delivery"))
                            voidAmount = vList.Where(v => v.OrderType.Equals("Delivery", StringComparison.OrdinalIgnoreCase)).Sum(v => v.TotalVoidedAmount);

                        t.Cell().Border(1).Padding(1).AlignCenter().Text(m.ModeTitle).FontSize(contentFontSize);
                        t.Cell().Border(1).Padding(1).AlignCenter().Text(m.OrderCount.ToString()).FontSize(contentFontSize);
                        t.Cell().Border(1).Padding(1).AlignCenter().Text(m.Discount.ToString("0.##")).FontSize(contentFontSize);
                        t.Cell().Border(1).Padding(1).AlignCenter().Text(voidAmount.ToString("0.##")).FontSize(contentFontSize);
                        t.Cell().Border(1).Padding(1).AlignCenter().Text(m.GrandTotal.ToString("0.##")).FontSize(contentFontSize);
                    }
                });

                // 5. Discounts & Voids
                ComposeSectionHeader(col, _isArabic ? "الخصومات والفويد" : "Discounts & Voids", headerFontSize);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                    AddFinancialRow(t, _isArabic ? "إجمالي الخصم" : "Total Discount", _summary.Overall.TotalDiscount.ToString("0.##"), contentFontSize);
                    AddFinancialRow(t, _isArabic ? "إجمالي الفويد" : "Total Void", _summary.Overall.VoidAmount.ToString("0.##"), contentFontSize);
                });

                // 6. Order Details Table (Match First Screenshot)
                ComposeSectionHeader(col, _isArabic ? "تفاصيل الأوردرات" : "Order Details", headerFontSize);
                if (_summary.DetailedOrders != null && _summary.DetailedOrders.Any())
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(3); cd.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "رقم" : "No").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "النوع" : "Type").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الإجمالي" : "Total").Bold().FontSize(contentFontSize);
                        });
                        foreach (var o in _summary.DetailedOrders)
                        {
                            t.Cell().Border(1).Padding(1).AlignCenter().Text($"#{o.OrderId}").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(o.OrderType).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(o.GrandTotal?.ToString("0.##")).FontSize(contentFontSize);
                        }
                    });
                }

                // 7. Item Details Table (Match First Screenshot)
                ComposeSectionHeader(col, _isArabic ? "تفاصيل الأصناف" : "Item Sales", headerFontSize);
                if (_catItems != null && _catItems.Any())
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(4); cd.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "كم" : "Qty").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الصنف" : "Item").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الإجمالي" : "Total").Bold().FontSize(contentFontSize);
                        });
                        foreach (var itm in _catItems)
                        {
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(itm.Quantity.ToString("G29")).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).Text(itm.ItemName).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignRight().Text(itm.TotalAmount.ToString("0.##")).FontSize(contentFontSize);
                        }
                        
                        t.Cell().ColumnSpan(2).Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الإجمالي الكلي" : "Grand Total").Bold().FontSize(9);
                        t.Cell().Border(1).Padding(1).AlignRight().Text(_catItems.Sum(x => x.TotalAmount).ToString("0.##")).Bold().FontSize(9);
                    });
                }

                var discountList = (_summary.DetailedOrders ?? new List<POS.Contract.Dtos.OrderDtos.OrderDto>())
                                    .Where(o => (o.TotalDiscount ?? 0) > 0 || (o.TotalOrderDiscount ?? 0) > 0 || (o.DiscountedItems ?? 0) > 0).ToList();
                if (discountList != null && discountList.Any())
                {
                    ComposeSectionHeader(col, _isArabic ? "تفاصيل الخصومات" : "Discount Details", headerFontSize);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { 
                            cd.RelativeColumn(1); // رقم
                            cd.RelativeColumn(2); // النوع
                            cd.RelativeColumn(2); // المستخدم
                            cd.RelativeColumn(3); // السبب
                            cd.RelativeColumn(2); // القيمة
                        });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "رقم" : "No").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "النوع" : "Type").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "المستخدم" : "User").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "السبب" : "Reason").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "القيمة" : "Amount").Bold().FontSize(contentFontSize);
                        });
                        foreach (var o in discountList)
                        {
                            var discountAmt = (o.TotalOrderDiscount ?? 0) > 0 ? o.TotalOrderDiscount : ((o.TotalDiscount ?? 0) > 0 ? o.TotalDiscount : o.DiscountedItems);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text($"#{o.OrderId}").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(o.OrderType ?? "-").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(string.IsNullOrEmpty(o.DiscountByName) ? (o.CashierName ?? "-") : o.DiscountByName).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(string.IsNullOrEmpty(o.DiscountReason) ? "-" : o.DiscountReason).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(discountAmt?.ToString("0.##")).FontSize(contentFontSize);
                        }
                    });
                }

                // 8. Void Details (Table Format)
                var extractedVoids = new List<VoidEventDto>();
                if (_summary.DetailedOrders != null)
                {
                    foreach (var o in _summary.DetailedOrders)
                    {
                        var itemVoids = (o.OrderDetails ?? new List<POS.Contract.Models.TableItem>())
                                        .Where(i => (i.VoidAmount ?? 0) > 0 || i.IsVoided).ToList();
                        
                        decimal sumItemVoids = 0;
                        foreach (var iv in itemVoids)
                        {
                            var amt = iv.VoidAmount ?? iv.Total ?? 0;
                            sumItemVoids += amt;
                            
                            var userName = !string.IsNullOrEmpty(iv.VoidByName) ? iv.VoidByName : (!string.IsNullOrEmpty(o.VoidByName) ? o.VoidByName : "System");
                            var reason = !string.IsNullOrEmpty(iv.VoidReason) ? iv.VoidReason : (!string.IsNullOrEmpty(o.VoidReason) ? o.VoidReason : "N/A");
                            
                            extractedVoids.Add(new VoidEventDto {
                                OrderId = o.OrderId,
                                OrderType = o.OrderType ?? "-",
                                VoidedByName = userName,
                                Reason = reason,
                                TotalVoidedAmount = amt
                            });
                        }

                        var orderVoidAmt = o.VoidAmount ?? (o.OrderState == "Voided" ? o.GrandTotal ?? 0 : 0);
                        if (orderVoidAmt > sumItemVoids + 0.05m)
                        {
                            extractedVoids.Add(new VoidEventDto {
                                OrderId = o.OrderId,
                                OrderType = o.OrderType ?? "-",
                                VoidedByName = !string.IsNullOrEmpty(o.VoidByName) ? o.VoidByName : "System",
                                Reason = !string.IsNullOrEmpty(o.VoidReason) ? o.VoidReason : "N/A",
                                TotalVoidedAmount = orderVoidAmt - sumItemVoids
                            });
                        }
                    }
                }

                if (extractedVoids.Any() || (_summary.Overall.VoidCount != 0 && _summary.Overall.VoidAmount > 0))
                {
                    // Group multiple void events for the same order into one row
                    var groupedVoids = extractedVoids
                        .GroupBy(v => v.OrderId)
                        .Select(g => new
                        {
                            OrderId = g.Key,
                            OrderType = g.First().OrderType,
                            Users = string.Join(" / ", g.Select(x => x.VoidedByName).Where(n => !string.IsNullOrEmpty(n) && n != "System").Distinct()),
                            Reasons = string.Join(" | ", g.Select(x => x.Reason).Where(r => !string.IsNullOrEmpty(r) && r != "N/A")),
                            TotalVoidMessage = g.Sum(x => x.TotalVoidedAmount)
                        }).ToList();

                    ComposeSectionHeader(col, _isArabic ? "تفاصيل الفويدات" : "Void Details", headerFontSize);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { 
                            cd.RelativeColumn(1); // رقم
                            cd.RelativeColumn(2); // النوع
                            cd.RelativeColumn(2); // المستخدم
                            cd.RelativeColumn(3); // السبب
                            cd.RelativeColumn(2); // القيمة
                        });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "رقم" : "No").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "النوع" : "Type").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "المستخدم" : "User").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "السبب" : "Reason").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "القيمة" : "Amount").Bold().FontSize(contentFontSize);
                        });
                        
                        foreach (var v in groupedVoids)
                        {
                            t.Cell().Border(1).Padding(1).AlignCenter().Text($"#{v.OrderId}").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(v.OrderType ?? "-").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(string.IsNullOrEmpty(v.Users) ? "-" : v.Users).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(string.IsNullOrEmpty(v.Reasons) ? "-" : v.Reasons).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(v.TotalVoidMessage.ToString("0.##")).FontSize(contentFontSize);
                        }
                    });
                }

                // 9. Staff Orders
                var staffOrders = (_summary.DetailedOrders ?? new List<Contract.Dtos.OrderDtos.OrderDto>())
                                  .Where(o => string.Equals(o.OrderType, "Staff", StringComparison.OrdinalIgnoreCase)).ToList();
                if (staffOrders.Any())
                {
                    ComposeSectionHeader(col, _isArabic ? "وجبات العاملين" : "Staff Meals", headerFontSize);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(2); cd.RelativeColumn(1); });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "رقم" : "No").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الموظف" : "Staff").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الوقت" : "Time").Bold().FontSize(contentFontSize);
                        });
                        foreach (var o in staffOrders)
                        {
                            t.Cell().Border(1).Padding(1).AlignCenter().Text($"#{o.OrderId}").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(!string.IsNullOrEmpty(o.StaffMealEmployeeName) ? o.StaffMealEmployeeName : (o.CustomerName ?? o.TakeAwayCustomerName ?? "-")).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(o.OrderDate?.ToString("HH:mm")).FontSize(contentFontSize);
                        }
                    });
                }

                // 10. Hospitality Orders
                var hospOrders = (_summary.DetailedOrders ?? new List<Contract.Dtos.OrderDtos.OrderDto>())
                                  .Where(o => string.Equals(o.OrderType, "Hospitality", StringComparison.OrdinalIgnoreCase)).ToList();
                if (hospOrders.Any())
                {
                    ComposeSectionHeader(col, _isArabic ? "طلبات الضيافة" : "Hospitality", headerFontSize);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(1.2f); cd.RelativeColumn(2.4f); cd.RelativeColumn(2.4f); cd.RelativeColumn(1.5f); });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "رقم" : "No").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "المسئول" : "Resp.").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "السبب" : "Reason").Bold().FontSize(contentFontSize);
                            h.Cell().Border(1).Padding(1).AlignCenter().Text(_isArabic ? "الوقت" : "Time").Bold().FontSize(contentFontSize);
                        });
                        foreach (var o in hospOrders)
                        {
                            t.Cell().Border(1).Padding(1).AlignCenter().Text($"#{o.OrderId}").FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(!string.IsNullOrEmpty(o.HospitalityResponsibleName) ? o.HospitalityResponsibleName : (o.CashierName ?? "-")).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(!string.IsNullOrEmpty(o.HospitalityReason) ? o.HospitalityReason : (o.CustomerName ?? o.TakeAwayCustomerName ?? "-")).FontSize(contentFontSize);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(o.OrderDate?.ToString("HH:mm")).FontSize(contentFontSize);
                        }
                    });
                }

                // 11. Printed Date
                col.Item().PaddingTop(10).AlignCenter().Text($"{DateTime.Now:HH:mm yyyy-MM-dd}").FontSize(7);
            });
        });
    }

    private void ComposeSectionHeader(ColumnDescriptor col, string title, float fontSize = 9)
    {
        col.Item().PaddingTop(5).Background(Colors.Black).Padding(2).AlignCenter().Text(title).FontColor(Colors.White).Bold().FontSize(fontSize);
    }

    private void AddFinancialRow(TableDescriptor t, string lbl, string val, float fontSize = 8)
    {
        t.Cell().Border(1).Padding(2).AlignCenter().Text(lbl).FontSize(fontSize).Bold();
        t.Cell().Border(1).Padding(2).AlignCenter().Text(val).FontSize(fontSize);
    }
}
