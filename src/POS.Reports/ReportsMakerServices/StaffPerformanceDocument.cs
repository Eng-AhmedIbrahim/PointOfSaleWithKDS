namespace POS.Reports.ReportsMakerServices;

public class StaffPerformanceDocument : IDocument
{
    private readonly SalesSummaryDto _summary;
    private readonly string _branchName;
    private readonly bool _isThermal;
    private readonly bool _isArabic;
    private readonly bool _showOrders;
    private readonly string? _specificStaffId; // If null, print all

    public StaffPerformanceDocument(SalesSummaryDto summary, string branchName, bool isThermal = false, bool isArabic = true, bool showOrders = false, string? specificStaffId = null)
    {
        _summary = summary;
        _branchName = branchName;
        _isThermal = isThermal;
        _isArabic = isArabic;
        _showOrders = showOrders;
        _specificStaffId = specificStaffId;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            if (_isThermal)
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
            page.DefaultTextStyle(x => x.FontFamily("Arial", "Noto Sans Arabic").FontSize(_isThermal ? 9 : 11));

            if (_isArabic)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 5 : 10);
                
                // 1. Header
                ComposeHeader(col);
                
                // 2. Content
                if (!string.IsNullOrEmpty(_specificStaffId))
                {
                    ComposeSingleStaff(col);
                }
                else
                {
                    ComposeAllStaff(col);
                }

                // 3. Footer
                col.Item().PaddingTop(10).AlignCenter().Text(text =>
                {
                    text.Span(_isArabic ? "طبع في: " : "Printed at: ").FontSize(8);
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(8);
                });
            });
        });
    }

    private void ComposeHeader(ColumnDescriptor col)
    {
        var title = _isArabic ? "تقرير أداء الموظفين" : "Staff Performance Report";
        if (!string.IsNullOrEmpty(_specificStaffId))
        {
            var staff = _summary.CashierSummaries.FirstOrDefault(s => s.Id == _specificStaffId);
            title = _isArabic ? $"تقرير الموظف: {staff?.Name}" : $"Staff Report: {staff?.Name}";
        }

        col.Item().AlignCenter().Text(title).FontSize(16).Bold().FontColor(Colors.Blue.Medium);
        
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
            t.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(3).Text(_isArabic ? "الفرع" : "Branch").Bold();
            t.Cell().Border(1).Padding(3).AlignRight().Text(_branchName);
            t.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(3).Text(_isArabic ? "التاريخ" : "Date").Bold();
            t.Cell().Border(1).Padding(3).AlignRight().Text(_summary.PosDate.ToString("yyyy-MM-dd"));
        });
    }

    private void ComposeAllStaff(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(3); // Name
                cd.RelativeColumn(1); // Qty
                cd.RelativeColumn(1.8f); // Cash
                cd.RelativeColumn(1.8f); // Visa
                cd.RelativeColumn(1.4f); // Void/Disc
                cd.RelativeColumn(2); // Total
            });

            t.Header(h =>
            {
                h.Cell().Padding(2).BorderBottom(1).Text(_isArabic ? "الموظف" : "Staff").Bold().FontSize(_isThermal ? 8 : 10);
                h.Cell().Padding(2).BorderBottom(1).AlignCenter().Text(_isArabic ? "عدد" : "Qty").Bold().FontSize(_isThermal ? 8 : 10);
                h.Cell().Padding(2).BorderBottom(1).AlignCenter().Text(_isArabic ? "كاش" : "Cash").Bold().FontSize(_isThermal ? 8 : 10);
                h.Cell().Padding(2).BorderBottom(1).AlignCenter().Text(_isArabic ? "فيزا" : "Visa").Bold().FontSize(_isThermal ? 8 : 10);
                h.Cell().Padding(2).BorderBottom(1).AlignCenter().Text(_isArabic ? "فويد/خصم" : "V/D").Bold().FontSize(_isThermal ? 8 : 10);
                h.Cell().Padding(2).BorderBottom(1).AlignRight().Text(_isArabic ? "الإجمالي" : "Total").Bold().FontSize(_isThermal ? 8 : 10);
            });

            foreach (var s in _summary.CashierSummaries)
            {
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).Text(s.Name).FontSize(_isThermal ? 8 : 10);
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignCenter().Text(s.OrderCount.ToString()).FontSize(_isThermal ? 8 : 10);
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignCenter().Text(s.CashAmount.ToString("0.##")).FontSize(_isThermal ? 8 : 10);
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignCenter().Text(s.CreditAmount.ToString("0.##")).FontSize(_isThermal ? 8 : 10);
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignCenter().Text((s.VoidAmount + s.DiscountAmount).ToString("0.##")).FontSize(_isThermal ? 8 : 10);
                t.Cell().Padding(2).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignRight().Text(s.TotalAmount.ToString("0.##")).Bold().FontSize(_isThermal ? 8 : 10);
            }
            
            // Grand totals
            t.Cell().Padding(2).Text(_isArabic ? "الإجمالي العام" : "Grand Total").Bold();
            t.Cell().Padding(2).AlignCenter().Text(_summary.CashierSummaries.Sum(x => x.OrderCount).ToString()).Bold();
            t.Cell().Padding(2).AlignCenter().Text(_summary.CashierSummaries.Sum(x => x.CashAmount).ToString("0.##")).Bold();
            t.Cell().Padding(2).AlignCenter().Text(_summary.CashierSummaries.Sum(x => x.CreditAmount).ToString("0.##")).Bold();
            t.Cell().Padding(2).AlignCenter().Text(_summary.CashierSummaries.Sum(x => x.VoidAmount + x.DiscountAmount).ToString("0.##")).Bold();
            t.Cell().Padding(2).AlignRight().Text(_summary.CashierSummaries.Sum(x => x.TotalAmount).ToString("0.##")).Bold();
        });
    }

    private void ComposeSingleStaff(ColumnDescriptor col)
    {
        var s = _summary.CashierSummaries.FirstOrDefault(x => x.Id == _specificStaffId);
        if (s == null) return;

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); });
            
            AddSummaryRow(t, _isArabic ? "عدد الطلبات" : "Order Count", s.OrderCount.ToString());
            AddSummaryRow(t, _isArabic ? "المبلغ النقدي" : "Cash Amount", s.CashAmount.ToString("0.##"));
            AddSummaryRow(t, _isArabic ? "مبلغ الفيزا" : "Visa/Card", s.CreditAmount.ToString("0.##"));
            AddSummaryRow(t, _isArabic ? "إجمالي الفويد" : "Total Void", s.VoidAmount.ToString("0.##"), Colors.Red.Medium);
            AddSummaryRow(t, _isArabic ? "إجمالي الخصم" : "Total Discount", s.DiscountAmount.ToString("0.##"), Colors.Green.Medium);
            
            t.Cell().Background(Colors.Grey.Lighten4).Padding(5).Text(_isArabic ? "صافي التحصيل" : "Net Collection").Bold().FontSize(12);
            t.Cell().Background(Colors.Grey.Lighten4).Padding(5).AlignRight().Text(s.TotalAmount.ToString("0.##")).Bold().FontSize(12);
        });

        if (_showOrders && _summary.DetailedOrders != null)
        {
            var staffOrders = _summary.DetailedOrders.Where(o => 
                (s.Type == "Cashier" && (o.CashierId == s.Id || o.CashierName == s.Name)) ||
                (s.Type == "Waiter"  && (o.WaiterId == s.Id  || o.WaiterName == s.Name))  ||
                (s.Type == "Driver"  && (o.DriverID == s.Id  || o.DriverName == s.Name))  ||
                // Fallback for types that might not have specific mapping yet
                (o.StaffId == s.Id || o.StaffName == s.Name) 
            ).ToList();
            if (staffOrders.Any())
            {
                col.Item().PaddingTop(10).Background(Colors.Grey.Darken2).Padding(3).AlignCenter().Text(_isArabic ? "قائمة الطلبات" : "Order List").FontColor(Colors.White).Bold();
                col.Item().Table(tab =>
                {
                    tab.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(1); // #
                        cd.RelativeColumn(1.5f); // Time
                        cd.RelativeColumn(2); // Type
                        cd.RelativeColumn(2); // Amount
                    });

                    tab.Header(h =>
                    {
                        h.Cell().Padding(1).BorderBottom(1).Text("#");
                        h.Cell().Padding(1).BorderBottom(1).Text(_isArabic ? "الوقت" : "Time");
                        h.Cell().Padding(1).BorderBottom(1).Text(_isArabic ? "النوع" : "Type");
                        h.Cell().Padding(1).BorderBottom(1).AlignRight().Text(_isArabic ? "المبلغ" : "Amount");
                    });

                    foreach (var o in staffOrders)
                    {
                        tab.Cell().Padding(1).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).Text($"#{o.OrderId}").FontSize(8);
                        tab.Cell().Padding(1).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).Text(o.OrderDate?.ToString("HH:mm") ?? "-").FontSize(8);
                        tab.Cell().Padding(1).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).Text(o.OrderType).FontSize(8);
                        tab.Cell().Padding(1).BorderBottom(1, Unit.Point).BorderColor(Colors.Grey.Lighten3).AlignRight().Text(o.GrandTotal?.ToString("0.##") ?? "0").FontSize(8);
                    }
                });
            }
        }
    }

    private void AddSummaryRow(TableDescriptor t, string label, string value, string? color = null)
    {
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(label);
        var text = t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text(value).Bold();
        if (color != null) text.FontColor(color);
    }
}
