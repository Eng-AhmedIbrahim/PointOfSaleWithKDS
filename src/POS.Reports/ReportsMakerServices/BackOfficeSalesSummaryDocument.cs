namespace POS.Reports.ReportsMakerServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

public class BackOfficeSalesSummaryDocument : IDocument
{
    private readonly SalesSummaryDto _summary;
    private readonly List<SalesItemSummaryDto> _catItems;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isThermal;
    private readonly string _lang;
    private readonly bool _isEnglish;

    public BackOfficeSalesSummaryDocument(SalesSummaryDto summary,
        List<SalesItemSummaryDto> catItems,
        string branchName, DateTime fromDate,
        DateTime toDate,
        bool isThermal = false,
        string lang = "ar")
    {
        _summary = summary;
        _catItems = catItems ?? new List<SalesItemSummaryDto>();
        _branchName = branchName;
        _fromDate = fromDate;
        _toDate = toDate;
        _isThermal = isThermal;
        _lang = (lang ?? "ar").ToLower();
        _isEnglish = _lang == "en";
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            if (_isThermal)
            {
                page.ContinuousSize(8f, Unit.Centimetre);
                page.Margin(3, Unit.Millimetre);
            }
            else
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
            }

            page.PageColor(Colors.White);

            var baseStyle = TextStyle.Default.FontFamily("Arial", "Segoe UI", "Tahoma", "Noto Sans Arabic")
                .FontSize(_isThermal ? 8.5f : 9.5f)
                .FontColor(Colors.Grey.Darken4);
            page.DefaultTextStyle(baseStyle);

            if (!_isEnglish)
                page.ContentFromRightToLeft();

            if (_isThermal)
            {
                page.Content().Column(col =>
                {
                    col.Spacing(4);
                    ComposeThermalHeader(col);
                    ComposeThermalFinancials(col);
                    ComposeThermalPayments(col);
                    ComposeThermalSalesByType(col);
                    ComposeThermalTopCategories(col);
                    ComposeThermalVoids(col);
                    ComposeThermalFooter(col);
                });
            }
            else
            {
                page.Header().Element(ComposeA4PageHeader);
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    ComposeA4KpiCards(col);
                    ComposeA4VisualCharts(col);
                    ComposeA4FinancialAndPayments(col);
                    ComposeA4SalesByType(col);
                    ComposeA4CategoryBreakdown(col);
                    ComposeA4TopItems(col);
                    if (_summary.CashierSummaries != null && _summary.CashierSummaries.Any())
                        ComposeA4EmployeeSales(col);
                    if (_summary.DetailedExpenses != null && _summary.DetailedExpenses.Any())
                        ComposeA4Expenses(col);
                    if (_summary.VoidEvents != null && _summary.VoidEvents.Any())
                        ComposeA4Voids(col);
                });
                page.Footer().Element(ComposeA4PageFooter);
            }
        });
    }

    // ==========================================
    // A4 HEADER / FOOTER (printed on every page)
    // ==========================================

    private void ComposeA4PageHeader(IContainer container)
    {
        container.PaddingBottom(6).BorderBottom(2).BorderColor(Colors.Blue.Darken2).Row(row =>
        {
            row.RelativeItem(7).Column(c =>
            {
                c.Item().Text(_branchName).FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                c.Item().Text(_isEnglish ? "EXECUTIVE SALES SUMMARY & ANALYTICS REPORT" : "تقرير ملخص المبيعات والتحليلات الشاملة").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                c.Item().PaddingTop(2).Text(_isEnglish
                    ? $"Period: {_fromDate:dd/MM/yyyy} to {_toDate:dd/MM/yyyy}"
                    : $"فترة التقرير: من {_fromDate:dd/MM/yyyy} إلى {_toDate:dd/MM/yyyy}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
            });

            row.RelativeItem(3).AlignRight().Column(c =>
            {
                var logoPath = GetLogoPath();
                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    c.Item().AlignRight().Width(60).Image(logoPath).FitWidth();
                c.Item().AlignRight().PaddingTop(4).Text(_isEnglish ? "Official POS Close" : "إغلاق رسمي معتمد").FontSize(7.5f).FontColor(Colors.Green.Darken2).Bold();
                c.Item().AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeA4PageFooter(IContainer container)
    {
        container.PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(r =>
        {
            r.RelativeItem().Text(_isEnglish
                ? $"Generated from POS System: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                : $"تم الاستخراج آلياً من نظام إدارة نقاط البيع: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            r.AutoItem().Text(text =>
            {
                text.Span(_isEnglish ? "Page " : "صفحة ").FontSize(7.5f);
                text.CurrentPageNumber().FontSize(7.5f).Bold();
                text.Span(_isEnglish ? " of " : " من ").FontSize(7.5f);
                text.TotalPages().FontSize(7.5f).Bold();
            });
        });
    }

    // ==========================================
    // A4 BODY SECTIONS
    // ==========================================

    private void ComposeA4KpiCards(ColumnDescriptor col)
    {
        int totalOrders = _summary.DineIn.OrderCount + _summary.TakeAway.OrderCount + _summary.Delivery.OrderCount;
        decimal avgCheck = totalOrders > 0 ? (_summary.Overall.TotalSales / totalOrders) : 0;

        col.Item().Row(r =>
        {
            r.Spacing(8);

            r.RelativeItem().Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten5).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "NET SALES" : "صافي المبيعات المحصلة").FontSize(8f).Bold().FontColor(Colors.Blue.Darken3);
                c.Item().Text(_summary.Overall.TotalSales.ToString("N2") + " ج.م").FontSize(13).Bold().FontColor(Colors.Blue.Darken4);
                c.Item().PaddingTop(2).Text(_isEnglish
                    ? $"Gross: {(_summary.Overall.TotalSales + _summary.Overall.TotalDiscount):N2} EGP"
                    : $"قبل الخصم: {(_summary.Overall.TotalSales + _summary.Overall.TotalDiscount):N2}").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
            });

            r.RelativeItem().Border(1).BorderColor(Colors.Teal.Lighten3).Background(Colors.Teal.Lighten5).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "ORDERS & AVG CHECK" : "الطلبات ومتوسط الفاتورة").FontSize(8f).Bold().FontColor(Colors.Teal.Darken3);
                c.Item().Text($"{totalOrders} " + (_isEnglish ? "Orders" : "طلب")).FontSize(13).Bold().FontColor(Colors.Teal.Darken4);
                c.Item().PaddingTop(2).Text(_isEnglish
                    ? $"Avg Check: {avgCheck:N2} EGP"
                    : $"متوسط الفاتورة: {avgCheck:N2} ج.م").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
            });

            r.RelativeItem().Border(1).BorderColor(Colors.Purple.Lighten3).Background(Colors.Purple.Lighten5).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "CASH & VISA" : "التحصيل (نقد وفـيزا)").FontSize(8f).Bold().FontColor(Colors.Purple.Darken3);
                c.Item().Text(_summary.Overall.CashAmount.ToString("N2") + " ج.م").FontSize(13).Bold().FontColor(Colors.Purple.Darken4);
                c.Item().PaddingTop(2).Text(_isEnglish
                    ? $"Visa: {_summary.Overall.CreditAmount:N2} EGP"
                    : $"الفيزا: {_summary.Overall.CreditAmount:N2} ج.م").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
            });

            r.RelativeItem().Border(1).BorderColor(Colors.Amber.Lighten3).Background(Colors.Amber.Lighten5).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "NET DRAWER CASH" : "صافي النقدية بالدرج").FontSize(8f).Bold().FontColor(Colors.Amber.Darken3);
                c.Item().Text(_summary.Overall.NetCash.ToString("N2") + " ج.م").FontSize(13).Bold().FontColor(Colors.Amber.Darken4);
                c.Item().PaddingTop(2).Text(_isEnglish
                    ? $"Expenses: {_summary.Overall.Expenses:N2} EGP"
                    : $"مصروفات الدرج: {_summary.Overall.Expenses:N2} ج.م").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private void ComposeA4VisualCharts(ColumnDescriptor col)
    {
        decimal totalChannelSales = _summary.DineIn.Total + _summary.TakeAway.Total + _summary.Delivery.Total + _summary.Staff.Total + _summary.Hospitality.Total;
        if (totalChannelSales <= 0) return;

        float dineInPct = (float)(_summary.DineIn.Total / totalChannelSales * 100);
        float takeAwayPct = (float)(_summary.TakeAway.Total / totalChannelSales * 100);
        float deliveryPct = (float)(_summary.Delivery.Total / totalChannelSales * 100);
        float otherPct = 100f - dineInPct - takeAwayPct - deliveryPct;
        if (otherPct < 0) otherPct = 0;

        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(c =>
        {
            c.Item().Text(_isEnglish ? "SALES CHANNEL DISTRIBUTION" : "توزيع المبيعات حسب قنوات البيع (تمثيل بياني)").Bold().FontSize(9.5f).FontColor(Colors.Blue.Darken3);

            c.Item().PaddingTop(6).Height(18).Row(bar =>
            {
                if (dineInPct > 0.5f)
                    bar.RelativeItem(Math.Max(1f, dineInPct)).Background(Colors.Blue.Medium)
                        .AlignCenter().AlignMiddle()
                        .Text($"{dineInPct:0.#}%").FontSize(7.5f).Bold().FontColor(Colors.White);
                if (takeAwayPct > 0.5f)
                    bar.RelativeItem(Math.Max(1f, takeAwayPct)).Background(Colors.Green.Medium)
                        .AlignCenter().AlignMiddle()
                        .Text($"{takeAwayPct:0.#}%").FontSize(7.5f).Bold().FontColor(Colors.White);
                if (deliveryPct > 0.5f)
                    bar.RelativeItem(Math.Max(1f, deliveryPct)).Background(Colors.Orange.Medium)
                        .AlignCenter().AlignMiddle()
                        .Text($"{deliveryPct:0.#}%").FontSize(7.5f).Bold().FontColor(Colors.White);
                if (otherPct > 0.5f)
                    bar.RelativeItem(Math.Max(1f, otherPct)).Background(Colors.Purple.Medium)
                        .AlignCenter().AlignMiddle()
                        .Text($"{otherPct:0.#}%").FontSize(7.5f).Bold().FontColor(Colors.White);
            });

            c.Item().PaddingTop(5).Row(leg =>
            {
                leg.Spacing(10);
                leg.RelativeItem().Row(r => {
                    r.ConstantItem(10).Height(10).Background(Colors.Blue.Medium);
                    r.RelativeItem().PaddingLeft(3).Text(_isEnglish
                        ? $"Dine-In: {_summary.DineIn.Total:N0} ({dineInPct:0.#}%)"
                        : $"صالة: {_summary.DineIn.Total:N0} ج.م ({dineInPct:0.#}%)").FontSize(8);
                });
                leg.RelativeItem().Row(r => {
                    r.ConstantItem(10).Height(10).Background(Colors.Green.Medium);
                    r.RelativeItem().PaddingLeft(3).Text(_isEnglish
                        ? $"Takeaway: {_summary.TakeAway.Total:N0} ({takeAwayPct:0.#}%)"
                        : $"تيك أواي: {_summary.TakeAway.Total:N0} ج.م ({takeAwayPct:0.#}%)").FontSize(8);
                });
                leg.RelativeItem().Row(r => {
                    r.ConstantItem(10).Height(10).Background(Colors.Orange.Medium);
                    r.RelativeItem().PaddingLeft(3).Text(_isEnglish
                        ? $"Delivery: {_summary.Delivery.Total:N0} ({deliveryPct:0.#}%)"
                        : $"دليفري: {_summary.Delivery.Total:N0} ج.م ({deliveryPct:0.#}%)").FontSize(8);
                });
                if (otherPct > 0.5f)
                    leg.RelativeItem().Row(r => {
                        r.ConstantItem(10).Height(10).Background(Colors.Purple.Medium);
                        r.RelativeItem().PaddingLeft(3).Text(_isEnglish ? "Other" : "أخرى").FontSize(8);
                    });
            });
        });
    }

    private void ComposeA4FinancialAndPayments(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Row(r =>
        {
            r.Spacing(10);

            r.RelativeItem().Column(c =>
            {
                c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "FINANCIAL BREAKDOWN" : "التفاصيل المالية والضريبية").Bold().FontSize(9).FontColor(Colors.White);
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(6); cd.RelativeColumn(4); });

                    decimal grossSales = _summary.Overall.TotalSales + _summary.Overall.TotalDiscount;

                    AddA4Row(t, _isEnglish ? "Gross Sales (Before Disc.)" : "إجمالي المبيعات (قبل الخصم)", grossSales.ToString("N2"), false);
                    AddA4Row(t, _isEnglish ? "Discounts Given" : "الخصومات الممنوحة", "-" + _summary.Overall.TotalDiscount.ToString("N2"), false, Colors.Red.Darken2);
                    if (_summary.Overall.TotalSalesTax > 0)
                        AddA4Row(t, _isEnglish ? "VAT / Sales Tax" : "ضريبة القيمة المضافة", _summary.Overall.TotalSalesTax.ToString("N2"), false);
                    if (_summary.Overall.ServiceTotal > 0)
                        AddA4Row(t, _isEnglish ? "Service & Delivery Fees" : "رسوم الخدمة والتوصيل", _summary.Overall.ServiceTotal.ToString("N2"), false);
                    AddA4Row(t, _isEnglish ? "Net Recognized Sales" : "صافي المبيعات المحققة", _summary.Overall.TotalSales.ToString("N2") + " ج.م", true, Colors.Blue.Darken3);
                });
            });

            r.RelativeItem().Column(c =>
            {
                c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "DRAWER RECONCILIATION" : "مطابقة الدرج وطرق الدفع").Bold().FontSize(9).FontColor(Colors.White);
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(6); cd.RelativeColumn(4); });

                    AddA4Row(t, _isEnglish ? "Cash Tendered" : "المحصل نقداً (كاش)", _summary.Overall.CashAmount.ToString("N2"), false);
                    AddA4Row(t, _isEnglish ? "Credit / Debit Cards" : "المحصل إلكترونياً (فيزا)", _summary.Overall.CreditAmount.ToString("N2"), false);
                    if (_summary.Overall.PendingAmount > 0)
                        AddA4Row(t, _isEnglish ? "Pending / Unsettled" : "طلبات معلقة أو تحت التحصيل", _summary.Overall.PendingAmount.ToString("N2"), false);
                    if (_summary.Overall.Expenses > 0)
                        AddA4Row(t, _isEnglish ? "Drawer Payouts (Expenses)" : "المصروفات المنصرفة من الدرج", "-" + _summary.Overall.Expenses.ToString("N2"), false, Colors.Orange.Darken3);
                    AddA4Row(t, _isEnglish ? "Expected Drawer Cash" : "النقدية المفترضة بالدرج", _summary.Overall.NetCash.ToString("N2") + " ج.م", true, Colors.Green.Darken3);
                });
            });
        });
    }

    private void ComposeA4SalesByType(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "SALES BY ORDER CHANNEL" : "تقرير المبيعات حسب قنوات الطلب").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(1.5f);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(1.5f);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Channel" : "القناة").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Orders" : "الطلبات").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Avg" : "متوسط").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Discount" : "الخصم").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Tax/Fees" : "ضرائب").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Net Total" : "الصافي").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text("%").Bold();
                });

                decimal grandTotal = _summary.DineIn.Total + _summary.TakeAway.Total + _summary.Delivery.Total + _summary.Staff.Total + _summary.Hospitality.Total;
                if (grandTotal <= 0) grandTotal = 1;

                AddChannelRow(t, _isEnglish ? "Dine-In" : "الصالة", _summary.DineIn.OrderCount, _summary.DineIn.Discount, _summary.DineIn.Tax + _summary.DineIn.Service, _summary.DineIn.Total, grandTotal);
                AddChannelRow(t, _isEnglish ? "Takeaway" : "تيك أواي", _summary.TakeAway.OrderCount, _summary.TakeAway.Discount, _summary.TakeAway.Tax + _summary.TakeAway.Service, _summary.TakeAway.Total, grandTotal);
                AddChannelRow(t, _isEnglish ? "Delivery" : "دليفري", _summary.Delivery.OrderCount, _summary.Delivery.Discount, _summary.Delivery.Tax + _summary.Delivery.DeliveryFees, _summary.Delivery.Total, grandTotal);
                if (_summary.Staff.OrderCount > 0 || _summary.Staff.Total > 0)
                    AddChannelRow(t, _isEnglish ? "Staff Meals" : "وجبات عاملين", _summary.Staff.OrderCount, _summary.Staff.Discount, 0, _summary.Staff.Total, grandTotal);
                if (_summary.Hospitality.OrderCount > 0 || _summary.Hospitality.Total > 0)
                    AddChannelRow(t, _isEnglish ? "Hospitality" : "ضيافة", _summary.Hospitality.OrderCount, _summary.Hospitality.Discount, 0, _summary.Hospitality.Total, grandTotal);
            });
        });
    }

    private void ComposeA4CategoryBreakdown(ColumnDescriptor col)
    {
        if (_catItems == null || !_catItems.Any()) return;

        var categories = _catItems.GroupBy(ci => ci.CategoryName)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount), Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Total).ToList();

        decimal grandTotal = categories.Sum(x => x.Total);
        if (grandTotal <= 0) return;

        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "CATEGORY SALES & SHARE" : "مبيعات الفئات ونسب المساهمة").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(4);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(4);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Category" : "الفئة").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Qty" : "الكمية").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Revenue" : "الإيراد").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text("%").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Chart" : "بياني").Bold();
                });

                foreach (var cat in categories.Take(10))
                {
                    float pct = (float)(cat.Total / grandTotal * 100);

                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(cat.Name).Bold();
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(cat.Qty.ToString("0.##"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(cat.Total.ToString("N2") + " ج.م").Bold();
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text($"{pct:0.#}%");
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignMiddle().Height(10).Row(r =>
                    {
                        r.RelativeItem(Math.Max(0.5f, pct)).Background(Colors.Blue.Medium);
                        if (pct < 99)
                            r.RelativeItem(100f - pct).Background(Colors.Grey.Lighten3);
                    });
                }
            });
        });
    }

    private void ComposeA4TopItems(ColumnDescriptor col)
    {
        if (_catItems == null || !_catItems.Any()) return;
        var topItems = _catItems.OrderByDescending(x => x.TotalAmount).Take(10).ToList();
        if (!topItems.Any()) return;

        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "TOP SELLING ITEMS" : "الأصناف الأكثر مبيعاً (حسب الإيراد)").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(22);
                    cd.RelativeColumn(5);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(3);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text("#").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Item Name" : "اسم الصنف").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Category" : "الفئة").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Qty" : "الكمية").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Unit Price" : "السعر").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Total" : "الإجمالي").Bold();
                });

                for (int i = 0; i < topItems.Count; i++)
                {
                    var itm = topItems[i];
                    var bg = (i % 2 == 0) ? Colors.White : Colors.Grey.Lighten5;

                    t.Cell().Background(bg).Padding(2).AlignCenter().Text((i + 1).ToString()).Bold().FontColor(Colors.Blue.Darken2);
                    t.Cell().Background(bg).Padding(2).Text(itm.ItemName).Bold();
                    t.Cell().Background(bg).Padding(2).AlignCenter().Text(itm.CategoryName);
                    t.Cell().Background(bg).Padding(2).AlignCenter().Text(itm.Quantity.ToString("0.##") + " " + itm.Unit);
                    t.Cell().Background(bg).Padding(2).AlignRight().Text(itm.UnitPrice.ToString("N2"));
                    t.Cell().Background(bg).Padding(2).AlignRight().Text(itm.TotalAmount.ToString("N2") + " ج.م").Bold();
                }
            });
        });
    }

    private void ComposeA4EmployeeSales(ColumnDescriptor col)
    {
        var cashiers = _summary.CashierSummaries
            .Where(s => string.IsNullOrEmpty(s.Type) || s.Type.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!cashiers.Any()) return;

        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "CASHIER PERFORMANCE" : "أداء الكاشير ومسئولي نقاط البيع").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(4);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(3);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Staff" : "الموظف").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Orders" : "الفواتير").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Cash" : "نقدي").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Visa" : "فيزا").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Disc" : "خصومات").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Void" : "إلغاءات").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Total" : "الإجمالي").Bold();
                });

                foreach (var s in cashiers)
                {
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(s.Name).Bold();
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(s.OrderCount.ToString());
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(s.CashAmount.ToString("N2"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(s.CreditAmount.ToString("N2"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(s.DiscountAmount.ToString("N2"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(s.VoidAmount.ToString("N2"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(s.TotalAmount.ToString("N2") + " ج.م").Bold();
                }
            });
        });
    }

    private void ComposeA4Expenses(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Grey.Darken3).Padding(5).Text(_isEnglish ? "EXPENSES & PAYOUTS LOG" : "سجل المصروفات والمنصرف من الدرج").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(5);
                    cd.RelativeColumn(2.5f);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Time" : "الوقت").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Category" : "البند").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Spent By" : "المستلم").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Description" : "البيان").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Amount" : "المبلغ").Bold();
                });

                foreach (var e in _summary.DetailedExpenses)
                {
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(e.Date.ToString("HH:mm"));
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(e.Category);
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(e.SpentBy ?? "-");
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(e.Description);
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(e.Amount.ToString("N2") + " ج.م").Bold();
                }
            });
        });
    }

    private void ComposeA4Voids(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Background(Colors.Red.Darken4).Padding(5).Text(_isEnglish ? "VOIDS & AUDIT LOG" : "سجل العمليات الملغاة (الأمن والتدقيق)").Bold().FontSize(9).FontColor(Colors.White);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2.5f);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(4.5f);
                    cd.RelativeColumn(2.5f);
                });

                t.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isEnglish ? "Order #" : "رقم الطلب").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Type" : "النوع").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Authorized By" : "المسئول").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(_isEnglish ? "Void Reason" : "سبب الإلغاء").Bold();
                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(_isEnglish ? "Voided Amt" : "المبلغ الملغى").Bold();
                });

                foreach (var v in _summary.VoidEvents)
                {
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text("#" + v.OrderId).Bold();
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(v.OrderType);
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(v.VoidedByName);
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(string.IsNullOrWhiteSpace(v.Reason) ? (_isEnglish ? "No reason" : "بدون سبب") : v.Reason);
                    t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(v.TotalVoidedAmount.ToString("N2") + " ج.م").Bold().FontColor(Colors.Red.Darken2);
                }
            });
        });
    }

    // ==========================================
    // THERMAL (80mm) SECTIONS — COMPACT, NO REPEAT
    // ==========================================

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold().FontColor(Colors.Black);
        col.Item().AlignCenter().Text(_isEnglish ? "Sales Summary (Z-Report)" : "ملخص المبيعات (تقرير الوردية)").FontSize(11).Bold();
        col.Item().AlignCenter().Text(_isEnglish
            ? $"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}"
            : $"الفترة: {_fromDate:dd/MM/yyyy} إلى {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalFinancials(ColumnDescriptor col)
    {
        col.Item().Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(_isEnglish ? "FINANCIAL SUMMARY" : "الملخص المالي").Bold().FontSize(9);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(5); cd.RelativeColumn(4); });

            decimal grossSales = _summary.Overall.TotalSales + _summary.Overall.TotalDiscount;
            AddThermalRow(t, _isEnglish ? "Gross Sales" : "إجمالي المبيعات", grossSales.ToString("N2"));
            if (_summary.Overall.TotalDiscount > 0)
                AddThermalRow(t, _isEnglish ? "Discounts" : "الخصومات", "-" + _summary.Overall.TotalDiscount.ToString("N2"));
            if (_summary.Overall.TotalSalesTax > 0)
                AddThermalRow(t, _isEnglish ? "Tax" : "الضريبة", _summary.Overall.TotalSalesTax.ToString("N2"));
            if (_summary.Overall.ServiceTotal > 0)
                AddThermalRow(t, _isEnglish ? "Service/Delivery" : "خدمة وتوصيل", _summary.Overall.ServiceTotal.ToString("N2"));

            t.Cell().PaddingVertical(2).BorderTop(1).Text(_isEnglish ? "Net Sales" : "صافي المبيعات").Bold().FontSize(9.5f);
            t.Cell().PaddingVertical(2).BorderTop(1).AlignRight().Text(_summary.Overall.TotalSales.ToString("N2") + " ج.م").Bold().FontSize(9.5f);
        });
    }

    private void ComposeThermalPayments(ColumnDescriptor col)
    {
        col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(_isEnglish ? "PAYMENTS & DRAWER" : "التحصيل والدرج").Bold().FontSize(9);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(5); cd.RelativeColumn(4); });

            AddThermalRow(t, _isEnglish ? "Cash" : "نقدية", _summary.Overall.CashAmount.ToString("N2"));
            AddThermalRow(t, _isEnglish ? "Visa/Card" : "دفع إلكتروني", _summary.Overall.CreditAmount.ToString("N2"));
            if (_summary.Overall.PendingAmount > 0)
                AddThermalRow(t, _isEnglish ? "Pending" : "معلق", _summary.Overall.PendingAmount.ToString("N2"));
            if (_summary.Overall.Expenses > 0)
                AddThermalRow(t, _isEnglish ? "Expenses" : "مصروفات الدرج", "-" + _summary.Overall.Expenses.ToString("N2"));

            t.Cell().PaddingVertical(2).BorderTop(1).Text(_isEnglish ? "Net Drawer" : "صافي الدرج").Bold();
            t.Cell().PaddingVertical(2).BorderTop(1).AlignRight().Text(_summary.Overall.NetCash.ToString("N2") + " ج.م").Bold();
        });
    }

    private void ComposeThermalSalesByType(ColumnDescriptor col)
    {
        col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(_isEnglish ? "ORDER CHANNELS" : "قنوات البيع").Bold().FontSize(9);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(4); cd.RelativeColumn(2); cd.RelativeColumn(3); });
            t.Header(h =>
            {
                h.Cell().PaddingBottom(2).Text(_isEnglish ? "Channel" : "النوع").Bold();
                h.Cell().PaddingBottom(2).AlignCenter().Text(_isEnglish ? "Qty" : "العدد").Bold();
                h.Cell().PaddingBottom(2).AlignRight().Text(_isEnglish ? "Total" : "الإجمالي").Bold();
            });

            if (_summary.DineIn.OrderCount > 0 || _summary.DineIn.Total > 0)
                AddThermalChannelRow(t, _isEnglish ? "Dine-In" : "صالة", _summary.DineIn.OrderCount, _summary.DineIn.Total);
            if (_summary.TakeAway.OrderCount > 0 || _summary.TakeAway.Total > 0)
                AddThermalChannelRow(t, _isEnglish ? "Takeaway" : "تيك أواي", _summary.TakeAway.OrderCount, _summary.TakeAway.Total);
            if (_summary.Delivery.OrderCount > 0 || _summary.Delivery.Total > 0)
                AddThermalChannelRow(t, _isEnglish ? "Delivery" : "دليفري", _summary.Delivery.OrderCount, _summary.Delivery.Total);
            if (_summary.Staff.OrderCount > 0 || _summary.Staff.Total > 0)
                AddThermalChannelRow(t, _isEnglish ? "Staff" : "عاملين", _summary.Staff.OrderCount, _summary.Staff.Total);
            if (_summary.Hospitality.OrderCount > 0 || _summary.Hospitality.Total > 0)
                AddThermalChannelRow(t, _isEnglish ? "Hospitality" : "ضيافة", _summary.Hospitality.OrderCount, _summary.Hospitality.Total);
        });
    }

    private void ComposeThermalTopCategories(ColumnDescriptor col)
    {
        if (_catItems == null || !_catItems.Any()) return;
        var topCats = _catItems.GroupBy(i => i.CategoryName)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Total).Take(5).ToList();
        if (!topCats.Any()) return;

        col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(_isEnglish ? "TOP CATEGORIES" : "أعلى الفئات مبيعاً").Bold().FontSize(9);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(6); cd.RelativeColumn(3); });
            foreach (var c in topCats)
            {
                t.Cell().PaddingVertical(1).Text(c.Name);
                t.Cell().PaddingVertical(1).AlignRight().Text(c.Total.ToString("N0") + " ج.م").Bold();
            }
        });
    }

    private void ComposeThermalVoids(ColumnDescriptor col)
    {
        if (_summary.Overall.VoidAmount <= 0 && _summary.Overall.VoidCount <= 0) return;

        col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(_isEnglish ? "VOIDS" : "ملخص الإلغاءات").Bold().FontSize(9);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(5); cd.RelativeColumn(4); });
            AddThermalRow(t, _isEnglish ? "Void Count" : "عدد الإلغاءات", _summary.Overall.VoidCount.ToString());
            AddThermalRow(t, _isEnglish ? "Total Voided" : "إجمالي الإلغاء", _summary.Overall.VoidAmount.ToString("N2") + " ج.م");
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text(text =>
        {
            text.Span(_isEnglish ? "Printed: " : "طبع: ").FontSize(7);
            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(7).Bold();
        });
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private void AddThermalRow(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(1).Text(label);
        t.Cell().PaddingVertical(1).AlignRight().Text(value).Bold();
    }

    private void AddThermalChannelRow(TableDescriptor t, string name, int count, decimal total)
    {
        t.Cell().PaddingVertical(1).Text(name);
        t.Cell().PaddingVertical(1).AlignCenter().Text(count.ToString());
        t.Cell().PaddingVertical(1).AlignRight().Text(total.ToString("N2")).Bold();
    }

    private void AddA4Row(TableDescriptor t, string label, string value, bool isTotal, string? color = null)
    {
        var bg = isTotal ? Colors.Grey.Lighten3 : Colors.White;

        var labelCell = t.Cell().Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(label);
        if (isTotal) labelCell.Bold();

        var valueCell = t.Cell().Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignRight().Text(value);
        if (isTotal) valueCell.Bold().FontSize(10);
        if (!string.IsNullOrEmpty(color)) valueCell.FontColor(color);
    }

    private void AddChannelRow(TableDescriptor t, string name, int count, decimal discount, decimal taxFees, decimal netTotal, decimal grandTotal)
    {
        float pct = grandTotal > 0 ? (float)(netTotal / grandTotal * 100) : 0;
        decimal avg = count > 0 ? (netTotal / count) : 0;

        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(name).Bold();
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignCenter().Text(count.ToString());
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignCenter().Text(avg.ToString("N1"));
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignCenter().Text(discount > 0 ? discount.ToString("N2") : "-");
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignCenter().Text(taxFees > 0 ? taxFees.ToString("N2") : "-");
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignRight().Text(netTotal.ToString("N2") + " ج.م").Bold();
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).AlignCenter().Text($"{pct:0.#}%").Bold();
    }

    private string GetLogoPath()
    {
        try
        {
            var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "ReceiptLogo");
            if (!Directory.Exists(folderPath))
                folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "ReceiptLogo");
            if (Directory.Exists(folderPath))
            {
                var file = Directory.GetFiles(folderPath, "*.*")
                    .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                return file ?? "";
            }
        }
        catch { }
        return "";
    }
}
