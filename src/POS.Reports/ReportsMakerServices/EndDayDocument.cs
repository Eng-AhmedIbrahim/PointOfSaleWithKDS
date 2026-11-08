namespace POS.Reports.ReportsMakerServices;

public class EndDayDocument : IDocument
{
    private readonly SalesSummaryDto _summary;
    private readonly List<SalesItemSummaryDto> _items;
    private readonly string _storeName;
    private readonly string _logoPath;
    private readonly ReportPageFormat _format;
    private readonly bool _isArabic;
    private readonly string _currency;

    public EndDayDocument(
        SalesSummaryDto summary,
        List<SalesItemSummaryDto> items,
        string storeName,
        string logoPath,
        ReportPageFormat format = ReportPageFormat.Cashier,
        bool isArabic = true,
        string currency = "EGP")
    {
        _summary = summary;
        _items = items;
        _storeName = storeName;
        _logoPath = logoPath;
        _format = format;
        _isArabic = isArabic;
        _currency = currency ?? "EGP";
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            if (_format == ReportPageFormat.Cashier)
            {
                page.ContinuousSize(8.0f, Unit.Centimetre);
                page.Margin(2, Unit.Millimetre);
            }
            else
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
            }

            page.PageColor(Colors.White);

            page.DefaultTextStyle(x =>
                x.FontFamily("Noto Sans", "Noto Sans Arabic")
                 .FontSize(_format == ReportPageFormat.Cashier ? 8 : 10));

            page.Content().PaddingVertical(5).Column(column =>
            {
                BuildHeader(column);
                BuildFinancialSummary(column);
                BuildModeBreakdown(column);
                BuildCashierSummaries(column);
                BuildItemsTable(column);
                
                if (_summary.DetailedOrders != null && _summary.DetailedOrders.Any(o => (o.TotalOrderDiscount ?? 0) > 0 || (o.TotalDiscount ?? 0) > 0))
                {
                    BuildDiscountsTable(column);
                }

                if (_summary.VoidEvents != null && _summary.VoidEvents.Any())
                {
                    BuildVoidsTable(column);
                }
            });

            page.Footer().Element(BuildFooter);
        });
    }

    private void BuildHeader(ColumnDescriptor column)
    {
        if (!string.IsNullOrWhiteSpace(_logoPath) && File.Exists(_logoPath))
        {
            column.Item().AlignCenter().Height(40).Image(_logoPath).FitArea();
        }

        column.Item().AlignCenter().Text(_storeName).FontSize(12).Bold();

        // Styled Title
        column.Item().PaddingVertical(4).Background(Colors.Black).PaddingVertical(2)
              .AlignCenter().Text(_isArabic ? "تقرير إنهاء يوم العمل" : "End of Work Day Report")
              .FontSize(11).Bold().FontColor(Colors.White);

        // Metadata Table
        column.Item().PaddingVertical(2).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            AddRowToTable(table, _isArabic ? "الفرع" : "Branch", _storeName);
            AddRowToTable(table, _isArabic ? "تاريخ العمل" : "Work Date", _summary.PosDate.ToString("yyyy-MM-dd"));
            AddRowToTable(table, _isArabic ? "وقت الطباعة" : "Print Time", DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"));
        });
        
        column.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void AddRowToTable(TableDescriptor table, string label, string value)
    {
        if (_isArabic)
        {
            table.Cell().AlignLeft().PaddingVertical(1).Text(value).FontSize(7);
            table.Cell().AlignRight().PaddingVertical(1).Text(label).FontSize(7).Bold();
        }
        else
        {
            table.Cell().AlignLeft().PaddingVertical(1).Text(label).FontSize(7).Bold();
            table.Cell().AlignRight().PaddingVertical(1).Text(value).FontSize(7);
        }
    }

    private void BuildFinancialSummary(ColumnDescriptor column)
    {
        column.Item().Element(c => SectionTitle(c, _isArabic ? "الملخص المالي" : "Financial Summary"));
        column.Item().PaddingHorizontal(2).Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            AddFinancialRow(t, _isArabic ? "إجمالي الإيرادات" : "Gross Revenue", _summary.Overall.TotalRevenue.ToString("0.##"), true);
            AddFinancialRow(t, _isArabic ? "إجمالي الخصومات" : "Total Discounts", _summary.Overall.TotalDiscount.ToString("0.##"));
            AddFinancialRow(t, _isArabic ? "صافي المبيعات" : "Net Sales", _summary.Overall.TotalSales.ToString("0.##"), true);
            
            AddFinancialRow(t, _isArabic ? "المحصل نقدي" : "Cash Collected", _summary.Overall.CashAmount.ToString("0.##"));
            AddFinancialRow(t, _isArabic ? "المحصل فيزا" : "Visa Collected", _summary.Overall.CreditAmount.ToString("0.##"));
            AddFinancialRow(t, _isArabic ? "المرتجعات" : "Refunds", _summary.Overall.RefundAmount.ToString("0.##"));
            AddFinancialRow(t, _isArabic ? "الملغيات" : "Voids", _summary.Overall.VoidAmount.ToString("0.##"));
            
            AddFinancialRow(t, _isArabic ? "صافي النقدية بالدرج" : "Net Cash in Drawer", _summary.Overall.NetCash.ToString("0.##"), true);
        });

        column.Item().PaddingTop(5).Element(c => SectionTitle(c, _isArabic ? "إحصائيات التشغيل" : "Operational Statistics"));
        column.Item().PaddingHorizontal(2).Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            AddFinancialRow(t, _isArabic ? "عدد الطلبات (الكل)" : "Total Orders", (_summary.DetailedOrders?.Count ?? 0).ToString());
            AddFinancialRow(t, _isArabic ? "عدد طلبات الصالة" : "Dine-In Orders", _summary.DineIn.OrderCount.ToString());
            AddFinancialRow(t, _isArabic ? "عدد طلبات التيك أواي" : "Take-Away Orders", _summary.TakeAway.OrderCount.ToString());
            AddFinancialRow(t, _isArabic ? "عدد طلبات التوصيل" : "Delivery Orders", _summary.Delivery.OrderCount.ToString());
        });
    }

    private void BuildModeBreakdown(ColumnDescriptor column)
    {
        column.Item().Element(c => SectionTitle(c, _isArabic ? "المبيعات حسب النوع" : "Sales Breakdown"));

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                if (_isArabic)
                {
                    cols.RelativeColumn(2); // Total
                    cols.RelativeColumn(1); // Discount
                    cols.RelativeColumn(1); // Void
                    cols.RelativeColumn(1); // Count
                    cols.RelativeColumn(2); // Type
                }
                else
                {
                    cols.RelativeColumn(2); // Type
                    cols.RelativeColumn(1); // Count
                    cols.RelativeColumn(1); // Void
                    cols.RelativeColumn(1); // Discount
                    cols.RelativeColumn(2); // Total
                }
            });

            table.Header(header =>
            {
                if (_isArabic)
                {
                    header.Cell().AlignLeft().Text("الإجمالي").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("خصم").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("فويد").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("عدد").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("النوع").Bold().FontSize(7);
                }
                else
                {
                    header.Cell().AlignLeft().Text("Type").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Qty").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Void").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Disc").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("Total").Bold().FontSize(7);
                }

                header.Cell().ColumnSpan(5).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Black);
            });

            AddModeRow(table, _summary.TakeAway, _isArabic ? "تيك أواي" : "Take-Away");
            AddModeRow(table, _summary.DineIn, _isArabic ? "صالة" : "Dine-In");
            AddModeRow(table, _summary.Delivery, _isArabic ? "توصيل" : "Delivery");
        });

        column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void AddModeRow(TableDescriptor table, ModeSummaryDto mode, string label)
    {
        if (mode.OrderCount == 0 && mode.Total == 0) return;

        if (_isArabic)
        {
            table.Cell().AlignLeft().Text(mode.Total.ToString("0.##")).FontSize(7);
            table.Cell().AlignCenter().Text(mode.Discount.ToString("0.##")).FontSize(7);
            table.Cell().AlignCenter().Text(mode.VoidAmount.ToString("0.##")).FontSize(7);
            table.Cell().AlignCenter().Text(mode.OrderCount.ToString()).FontSize(7);
            table.Cell().AlignRight().Text(label).FontSize(7);
        }
        else
        {
            table.Cell().AlignLeft().Text(label).FontSize(7);
            table.Cell().AlignCenter().Text(mode.OrderCount.ToString()).FontSize(7);
            table.Cell().AlignCenter().Text(mode.VoidAmount.ToString("0.##")).FontSize(7);
            table.Cell().AlignCenter().Text(mode.Discount.ToString("0.##")).FontSize(7);
            table.Cell().AlignRight().Text(mode.Total.ToString("0.##")).FontSize(7);
        }
        
        table.Cell().ColumnSpan(5).PaddingVertical(0.5f).LineHorizontal(0.1f).LineColor(Colors.Grey.Lighten3);
    }

    private void BuildCashierSummaries(ColumnDescriptor column)
    {
        if (_summary.CashierSummaries == null || !_summary.CashierSummaries.Any()) return;

        column.Item().PaddingTop(4).PaddingBottom(2).AlignCenter()
              .Text(_isArabic ? "--- مبيعات الموظفين ---" : "--- Staff Sales Summary ---")
              .Bold().FontSize(9);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                if (_isArabic)
                {
                    cols.RelativeColumn(2); // Total
                    cols.RelativeColumn(1); // Void
                    cols.RelativeColumn(1); // Disc
                    cols.RelativeColumn(3); // Name
                }
                else
                {
                    cols.RelativeColumn(3); // Name
                    cols.RelativeColumn(1); // Disc
                    cols.RelativeColumn(1); // Void
                    cols.RelativeColumn(2); // Total
                }
            });

            table.Header(header =>
            {
                if (_isArabic)
                {
                    header.Cell().AlignLeft().Text("الإجمالي").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("فويد").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("خصم").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("الموظف").Bold().FontSize(7);
                }
                else
                {
                    header.Cell().AlignLeft().Text("Cashier").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Disc").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Void").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("Total").Bold().FontSize(7);
                }

                header.Cell().ColumnSpan(4).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Black);
            });

            foreach (var cashier in _summary.CashierSummaries)
            {
                if (_isArabic)
                {
                    table.Cell().AlignLeft().Text(cashier.TotalAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignCenter().Text(cashier.VoidAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignCenter().Text(cashier.DiscountAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignRight().Text(cashier.Name).FontSize(7);
                }
                else
                {
                    table.Cell().AlignLeft().Text(cashier.Name).FontSize(7);
                    table.Cell().AlignCenter().Text(cashier.DiscountAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignCenter().Text(cashier.VoidAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignRight().Text(cashier.TotalAmount.ToString("0.##")).FontSize(7);
                }
                table.Cell().ColumnSpan(4).PaddingVertical(0.5f).LineHorizontal(0.1f).LineColor(Colors.Grey.Lighten3);
            }
        });

        column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void BuildItemsTable(ColumnDescriptor column)
    {
        if (_items == null || !_items.Any()) return;

        column.Item().PaddingTop(4).PaddingBottom(2).AlignCenter()
              .Text(_isArabic ? "--- ملخص الأصناف المباعة ---" : "--- Sold Items Summary ---")
              .Bold().FontSize(9);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                if (_isArabic)
                {
                    cols.RelativeColumn(2); // Total
                    cols.RelativeColumn(4); // Item
                    cols.RelativeColumn(1); // Qty
                }
                else
                {
                    cols.RelativeColumn(1); // Qty
                    cols.RelativeColumn(4); // Item
                    cols.RelativeColumn(2); // Total
                }
            });

            table.Header(header =>
            {
                if (_isArabic)
                {
                    header.Cell().AlignLeft().Text("الإجمالي").FontSize(7).Bold();
                    header.Cell().AlignRight().Text("الصنف").FontSize(7).Bold();
                    header.Cell().AlignCenter().Text("الكمية").FontSize(7).Bold();
                }
                else
                {
                    header.Cell().AlignCenter().Text("Qty").FontSize(7).Bold();
                    header.Cell().AlignLeft().Text("Item").FontSize(7).Bold();
                    header.Cell().AlignRight().Text("Total").FontSize(7).Bold();
                }
                header.Cell().ColumnSpan(3).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Black);
            });

            foreach (var item in _items.OrderByDescending(i => i.Quantity))
            {
                if (_isArabic)
                {
                    table.Cell().AlignLeft().Text(item.TotalAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignRight().Text(item.ItemName).FontSize(7).Bold();
                    table.Cell().AlignCenter().Text(item.Quantity.ToString("0.##")).FontSize(7);
                }
                else
                {
                    table.Cell().AlignCenter().Text(item.Quantity.ToString("0.##")).FontSize(7);
                    table.Cell().AlignLeft().Text(item.ItemName).FontSize(7);
                    table.Cell().AlignRight().Text(item.TotalAmount.ToString("0.##")).FontSize(7);
                }
                table.Cell().ColumnSpan(3).PaddingVertical(0.5f).LineHorizontal(0.1f).LineColor(Colors.Grey.Lighten4);
            }
            
            // Grand Total Row
            table.Footer(footer =>
            {
                var totalQty = _items.Sum(i => i.Quantity);
                var totalAmount = _items.Sum(i => i.TotalAmount);
                
                footer.Cell().ColumnSpan(3).PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Black);
                
                if (_isArabic)
                {
                    footer.Cell().AlignLeft().Text(totalAmount.ToString("0.##")).FontSize(7).Bold();
                    footer.Cell().AlignRight().Text("الإجمالي").FontSize(7).Bold();
                    footer.Cell().AlignCenter().Text(totalQty.ToString("0.##")).FontSize(7).Bold();
                }
                else
                {
                    footer.Cell().AlignCenter().Text(totalQty.ToString("0.##")).FontSize(7).Bold();
                    footer.Cell().AlignLeft().Text("Grand Total").FontSize(7).Bold();
                    footer.Cell().AlignRight().Text(totalAmount.ToString("0.##")).FontSize(7).Bold();
                }
            });
        });
        column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void BuildDiscountsTable(ColumnDescriptor column)
    {
        column.Item().PaddingTop(4).PaddingBottom(2).AlignCenter()
              .Text(_isArabic ? "--- ملخص الخصومات ---" : "--- Discount Summary ---")
              .Bold().FontSize(9);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                if (_isArabic)
                {
                    cols.RelativeColumn(2); // Amt
                    cols.RelativeColumn(2); // Type
                    cols.RelativeColumn(2); // User
                    cols.RelativeColumn(1.5f); // Time
                    cols.RelativeColumn(1); // Order
                }
                else
                {
                    cols.RelativeColumn(1); // Order
                    cols.RelativeColumn(1.5f); // Time
                    cols.RelativeColumn(2); // User
                    cols.RelativeColumn(2); // Type
                    cols.RelativeColumn(2); // Amt
                }
            });

            table.Header(header => {
                if (_isArabic) {
                    header.Cell().AlignLeft().Text("المبلغ").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("النوع").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("الموظف").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("الوقت").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("رقم").Bold().FontSize(7);
                } else {
                    header.Cell().AlignLeft().Text("ID").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Time").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("User").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Type").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("Amt").Bold().FontSize(7);
                }
                header.Cell().ColumnSpan(5).PaddingVertical(1).LineHorizontal(0.5f);
            });

            foreach (var order in _summary.DetailedOrders!.Where(o => (o.TotalOrderDiscount ?? 0) > 0 || (o.TotalDiscount ?? 0) > 0))
            {
                var discountAmt = (order.TotalOrderDiscount ?? 0) + (order.TotalDiscount ?? 0);
                var timeStr = (order.DiscountTime ?? order.OrderDate)?.ToString("hh:mm tt") ?? "--:--";
                var user = order.DiscountByName ?? order.CashierName ?? "System";
                var type = order.OrderType ?? "N/A";

                if (_isArabic) {
                    table.Cell().AlignLeft().Text(discountAmt.ToString("0.##")).FontSize(7);
                    table.Cell().AlignCenter().Text(type).FontSize(7);
                    table.Cell().AlignCenter().Text(user).FontSize(7);
                    table.Cell().AlignCenter().Text(timeStr).FontSize(7);
                    table.Cell().AlignRight().Text($"#{order.OrderId}").FontSize(7);
                } else {
                    table.Cell().AlignLeft().Text($"#{order.OrderId}").FontSize(7);
                    table.Cell().AlignCenter().Text(timeStr).FontSize(7);
                    table.Cell().AlignCenter().Text(user).FontSize(7);
                    table.Cell().AlignCenter().Text(type).FontSize(7);
                    table.Cell().AlignRight().Text(discountAmt.ToString("0.##")).FontSize(7);
                }
                table.Cell().ColumnSpan(5).PaddingVertical(0.5f).LineHorizontal(0.1f).LineColor(Colors.Grey.Lighten4);
            }
        });
        column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void BuildVoidsTable(ColumnDescriptor column)
    {
        column.Item().Element(c => SectionTitle(c, _isArabic ? "ملخص الإلغاءات" : "Void Summary"));

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                if (_isArabic)
                {
                    cols.RelativeColumn(2); // Amount
                    cols.RelativeColumn(2); // Type
                    cols.RelativeColumn(2); // User
                    cols.RelativeColumn(1.5f); // Time
                    cols.RelativeColumn(1); // Order
                }
                else
                {
                    cols.RelativeColumn(1); // Order
                    cols.RelativeColumn(1.5f); // Time
                    cols.RelativeColumn(2); // User
                    cols.RelativeColumn(2); // Type
                    cols.RelativeColumn(2); // Amount
                }
            });

            table.Header(header => {
                if (_isArabic) {
                    header.Cell().AlignLeft().Text("المبلغ").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("النوع").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("الموظف").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("الوقت").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("رقم").Bold().FontSize(7);
                } else {
                    header.Cell().AlignLeft().Text("ID").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Time").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("User").Bold().FontSize(7);
                    header.Cell().AlignCenter().Text("Type").Bold().FontSize(7);
                    header.Cell().AlignRight().Text("Amt").Bold().FontSize(7);
                }
                header.Cell().ColumnSpan(5).PaddingVertical(1).LineHorizontal(0.5f);
            });

            foreach (var v in _summary.VoidEvents)
            {
                var timeStr = v.VoidDate.ToString("hh:mm tt");
                if (_isArabic) {
                    table.Cell().AlignLeft().Text(v.TotalVoidedAmount.ToString("0.##")).FontSize(7);
                    table.Cell().AlignCenter().Text(v.OrderType).FontSize(7);
                    table.Cell().AlignCenter().Text(v.VoidedByName).FontSize(7);
                    table.Cell().AlignCenter().Text(timeStr).FontSize(7);
                    table.Cell().AlignRight().Text($"#{v.OrderId}").FontSize(7);
                } else {
                    table.Cell().AlignLeft().Text($"#{v.OrderId}").FontSize(7);
                    table.Cell().AlignCenter().Text(timeStr).FontSize(7);
                    table.Cell().AlignCenter().Text(v.VoidedByName).FontSize(7);
                    table.Cell().AlignCenter().Text(v.OrderType).FontSize(7);
                    table.Cell().AlignRight().Text(v.TotalVoidedAmount.ToString("0.##")).FontSize(7);
                }
                table.Cell().ColumnSpan(5).PaddingVertical(0.5f).LineHorizontal(0.1f).LineColor(Colors.Grey.Lighten4);
            }
        });
        column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void SectionTitle(IContainer container, string title)
    {
        container.PaddingVertical(1).Column(col =>
        {
            col.Item().Background(Colors.Grey.Lighten3).PaddingHorizontal(4).PaddingVertical(1).Row(row =>
            {
                if (_isArabic)
                    row.RelativeItem().AlignRight().Text(title).FontSize(9).Bold().FontColor(Colors.Black);
                else
                    row.RelativeItem().AlignLeft().Text(title).FontSize(9).Bold().FontColor(Colors.Black);
            });
            col.Item().Height(1).Background(Colors.Grey.Lighten1);
        });
    }

    private void AddFinancialRow(TableDescriptor table, string label, string value, bool bold = false)
    {
        if (_isArabic)
        {
            if (bold)
            {
                table.Cell().Border(1).Padding(1).AlignLeft().Text(value).FontSize(7).Bold();
                table.Cell().Border(1).Padding(1).AlignRight().Text(label).FontSize(7).Bold();
            }
            else
            {
                table.Cell().Border(1).Padding(1).AlignLeft().Text(value).FontSize(7);
                table.Cell().Border(1).Padding(1).AlignRight().Text(label).FontSize(7);
            }
        }
        else
        {
            if (bold)
            {
                table.Cell().Border(1).Padding(1).AlignLeft().Text(label).FontSize(7).Bold();
                table.Cell().Border(1).Padding(1).AlignRight().Text(value).FontSize(7).Bold();
            }
            else
            {
                table.Cell().Border(1).Padding(1).AlignLeft().Text(label).FontSize(7);
                table.Cell().Border(1).Padding(1).AlignRight().Text(value).FontSize(7);
            }
        }
    }

    private void BuildFooter(IContainer container)
    {
        container.PaddingTop(5).AlignCenter().Column(col => {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
            col.Item().PaddingTop(2).AlignCenter().Text(x =>
            {
                x.Span(_isArabic ? "تم الطباعة في: " : "Printed at: ").FontSize(7).FontColor(Colors.Grey.Darken1);
                x.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken1);
                if (_format == ReportPageFormat.A4)
                {
                    x.EmptyLine();
                    x.Span($"{(_isArabic ? "صفحة " : "Page ")}").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                }
            });
            col.Item().AlignCenter().Text(_isArabic ? "شكراً لاستخدامكم نظام إدارة المبيعات" : "Thank you for using our POS System").FontSize(6).FontColor(Colors.Grey.Lighten2);
        });
    }
}
