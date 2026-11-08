using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

public class DriverBonusItem
{
    public string DriverId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal DeliveryFees { get; set; }
    public decimal TotalBonus { get; set; }
    public decimal AverageBonusPerOrder => OrderCount > 0 ? TotalBonus / OrderCount : 0;
    public decimal BonusPercentage { get; set; }
    public bool IsTopDriver { get; set; }
}

public class DeliveryOrderRecord
{
    public int OrderId { get; set; }
    public DateTime? OrderDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public decimal DeliveryFees { get; set; }
    public decimal Bonus { get; set; }
    public decimal GrandTotal { get; set; }
}

public class BackOfficeDeliveryBonusDocument : IDocument
{
    private readonly List<DriverBonusItem> _drivers;
    private readonly List<DeliveryOrderRecord> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeDeliveryBonusDocument(
        List<DriverBonusItem> drivers,
        List<DeliveryOrderRecord> orders,
        string branchName,
        DateTime fromDate,
        DateTime toDate,
        bool isThermal,
        string? language)
    {
        _drivers = drivers ?? new List<DriverBonusItem>();
        _orders = orders ?? new List<DeliveryOrderRecord>();
        _branchName = branchName;
        _fromDate = fromDate;
        _toDate = toDate;
        _isThermal = isThermal;
        _isEnglish = (language ?? "ar").ToLower() == "en";
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
                page.Margin(1.2f, Unit.Centimetre);
                page.Header().Element(ComposeA4Header);
                page.Footer().Element(ComposeA4Footer);
            }

            page.PageColor(Colors.White);
            var baseStyle = TextStyle.Default
                .FontFamily("Arial", "Noto Sans Arabic")
                .FontSize(_isThermal ? 8 : 9);
            page.DefaultTextStyle(baseStyle);

            if (!_isEnglish)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 3 : 5);
                if (_isThermal)
                {
                    ComposeThermalHeader(col);
                    ComposeThermalContent(col);
                    ComposeThermalFooter(col);
                }
                else
                {
                    ComposeA4KpiCards(col);
                    ComposeA4DriversTable(col);
                    ComposeA4OrdersTable(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Amber.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Delivery Service Fees & Driver Bonus Report" : "تقرير إجمالي خدمة التوصيل وبونص الطيارين")
                        .FontSize(10).FontColor(Colors.Amber.Lighten4);
                });
                row.ConstantItem(210).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"الفترة من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Amber.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Delivery Service & Driver Bonus" : "خدمة التوصيل وبونص الطيارين")
                .FontSize(8).FontColor(Colors.Grey.Medium);
            row.ConstantItem(100).AlignRight().Text(txt =>
            {
                txt.Span(_isEnglish ? "Page " : "صفحة ").FontSize(8).FontColor(Colors.Grey.Medium);
                txt.CurrentPageNumber().FontSize(8).Bold();
                txt.Span(_isEnglish ? " of " : " من ").FontSize(8).FontColor(Colors.Grey.Medium);
                txt.TotalPages().FontSize(8).Bold();
            });
        });
    }

    private void ComposeA4KpiCards(ColumnDescriptor col)
    {
        int totalOrders = _orders.Count;
        decimal totalFees = _orders.Sum(o => o.DeliveryFees);
        decimal totalBonus = _orders.Sum(o => o.Bonus);
        var topDriver = _drivers.OrderByDescending(d => d.TotalBonus).FirstOrDefault(d => d.TotalBonus > 0);
        decimal avgFee = totalOrders > 0 ? totalFees / totalOrders : 0;

        col.Item().PaddingBottom(6).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Delivery Orders" : "طلبات التوصيل",
                $"{totalOrders:N0}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Delivery Fees" : "إجمالي خدمة التوصيل",
                $"{totalFees:N2} {Currency}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Drivers Bonus" : "إجمالي البونص المستحق",
                $"{totalBonus:N2} {Currency}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(1.3f), _isEnglish ? "Top Driver Bonus" : "أعلى طيار بونص 🛵",
                topDriver != null ? $"{topDriver.DriverName} ({topDriver.TotalBonus:N0} {Currency})" : "-",
                Colors.Amber.Darken4, Colors.Amber.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg Fee / Order" : "متوسط الخدمة / طلب",
                $"{avgFee:N2} {Currency}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
        });
    }

    private static void KpiCard(IContainer c, string label, string value, Color textColor, Color bgColor)
    {
        c.Background(bgColor).Border(1).BorderColor(textColor).Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(7.5f).FontColor(textColor);
            col.Item().Text(value).FontSize(9.5f).Bold().FontColor(textColor);
        });
    }

    private void ComposeA4DriversTable(ColumnDescriptor col)
    {
        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Drivers Bonus Breakdown (Distribution per Driver)" : "توزيع البونص وخدمة التوصيل على الطيارين")
                .FontSize(10).Bold().FontColor(Colors.Amber.Darken4);

            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(24);   // #
                    cd.RelativeColumn(2f);   // Driver Name
                    cd.RelativeColumn(1f);   // Orders
                    cd.RelativeColumn(1.3f); // Sales
                    cd.RelativeColumn(1.2f); // Delivery Fees
                    cd.RelativeColumn(1.2f); // Bonus
                    cd.RelativeColumn(1f);   // Avg Bonus / Order
                    cd.RelativeColumn(0.9f); // % Share
                    cd.RelativeColumn(1f);   // Badge
                });

                void HCell(IContainer c, string text)
                {
                    c.Background(Colors.Amber.Darken3).Padding(4).AlignCenter().Text(text)
                        .FontSize(7.5f).Bold().FontColor(Colors.White);
                }

                t.Header(h =>
                {
                    h.Cell().Element(c => HCell(c, "#"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Driver Name" : "اسم الطيار"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Orders" : "عدد الطلبات"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Total Sales" : "إجمالي المبيعات"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Delivery Fees" : "خدمة التوصيل"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Total Bonus" : "إجمالي البونص"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Avg Bonus" : "متوسط البونص"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "% Share" : "نسبة البونص"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Status" : "الترتيب"));
                });

                int idx = 1;
                foreach (var d in _drivers.OrderByDescending(x => x.TotalBonus))
                {
                    var bg = d.IsTopDriver ? Colors.Amber.Lighten5 : (idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White);

                    void DCell(IContainer c, string text, bool bold = false, Color? color = null)
                    {
                        var txt = c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(3).AlignCenter().Text(text ?? "").FontSize(7.5f);
                        if (bold) txt.Bold();
                        if (color.HasValue) txt.FontColor(color.Value);
                    }

                    DCell(t.Cell(), idx.ToString());
                    DCell(t.Cell(), d.DriverName ?? "", bold: true, color: d.IsTopDriver ? (Color?)Colors.Amber.Darken4 : null);
                    DCell(t.Cell(), d.OrderCount.ToString("N0"));
                    DCell(t.Cell(), $"{d.TotalSales:N2}");
                    DCell(t.Cell(), $"{d.DeliveryFees:N2}", bold: true, color: (Color?)Colors.Teal.Darken3);
                    DCell(t.Cell(), $"{d.TotalBonus:N2}", bold: true, color: (Color?)Colors.Green.Darken3);
                    DCell(t.Cell(), $"{d.AverageBonusPerOrder:N2}", bold: true, color: (Color?)Colors.Green.Darken3);
                    DCell(t.Cell(), $"{d.BonusPercentage:N1}%", bold: true, color: (Color?)Colors.Green.Darken3);
                    DCell(t.Cell(), d.IsTopDriver ? "\u0623\u0639\u0644\u0649 \u0628\u0648\u0646\u0635" : $"#{idx}", bold: d.IsTopDriver, color: d.IsTopDriver ? (Color?)Colors.Amber.Darken3 : (Color?)Colors.Grey.Darken1);

                    idx++;
                }

                // Totals
                void TCell(IContainer c, string text)
                {
                    c.Background(Colors.Amber.Lighten5).BorderTop(1.5f).BorderColor(Colors.Amber.Darken2)
                        .Padding(4).AlignCenter().Text(text).FontSize(8).Bold().FontColor(Colors.Amber.Darken4);
                }

                int totalOrders = _drivers.Sum(d => d.OrderCount);
                decimal totalSales = _drivers.Sum(d => d.TotalSales);
                decimal totalFees = _drivers.Sum(d => d.DeliveryFees);
                decimal totalBonus = _drivers.Sum(d => d.TotalBonus);
                decimal avgBonus = totalOrders > 0 ? totalBonus / totalOrders : 0;

                t.Cell().Element(c => TCell(c, ""));
                t.Cell().Element(c => TCell(c, _isEnglish ? "Total" : "الإجمالي"));
                t.Cell().Element(c => TCell(c, totalOrders.ToString("N0")));
                t.Cell().Element(c => TCell(c, $"{totalSales:N2}"));
                t.Cell().Element(c => TCell(c, $"{totalFees:N2}"));
                t.Cell().Element(c => TCell(c, $"{totalBonus:N2}"));
                t.Cell().Element(c => TCell(c, $"{avgBonus:N2}"));
                t.Cell().Element(c => TCell(c, "100%"));
                t.Cell().Element(c => TCell(c, "-"));
            });
        });
    }

    private void ComposeA4OrdersTable(ColumnDescriptor col)
    {
        if (!_orders.Any()) return;

        col.Item().PaddingTop(4).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Delivery Orders Details" : "سجل طلبات التوصيل بالتفصيل")
                .FontSize(9.5f).Bold().FontColor(Colors.Grey.Darken3);

            inner.Item().PaddingTop(3).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(30);   // Order #
                    cd.ConstantColumn(80);   // Date/Time
                    cd.RelativeColumn(1.4f); // Customer
                    cd.RelativeColumn(1.2f); // Zone
                    cd.RelativeColumn(1.2f); // Driver
                    cd.RelativeColumn(1f);   // Delivery Fee
                    cd.RelativeColumn(1f);   // Bonus
                    cd.RelativeColumn(1.1f); // Grand Total
                });

                void HCell(IContainer c, string text)
                {
                    c.Background(Colors.Grey.Darken2).Padding(3).AlignCenter().Text(text)
                        .FontSize(7).Bold().FontColor(Colors.White);
                }

                t.Header(h =>
                {
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Order#" : "رقم الطلب"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Time" : "الوقت"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Customer" : "العميل"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Zone" : "المنطقة"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Driver" : "الطيار"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Fee" : "رسوم التوصيل"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Bonus" : "البونص"));
                    h.Cell().Element(c => HCell(c, _isEnglish ? "Total" : "الإجمالي"));
                });

                int row = 0;
                foreach (var o in _orders.Take(100)) // Cap at 100 for display
                {
                    var bg = row % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                    void DCell(IContainer c, string text, bool bold = false, Color? color = null)
                    {
                        var txt = c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2.5f).AlignCenter().Text(text).FontSize(7);
                        if (bold) txt.Bold();
                        if (color.HasValue) txt.FontColor(color.Value);
                    }

                    DCell(t.Cell(), $"#{o.OrderId}");
                    DCell(t.Cell(), o.OrderDate.HasValue ? o.OrderDate.Value.ToString("dd/MM HH:mm") : "-");
                    DCell(t.Cell(), !string.IsNullOrWhiteSpace(o.CustomerName) ? o.CustomerName : o.Phone);
                    DCell(t.Cell(), !string.IsNullOrWhiteSpace(o.ZoneName) ? o.ZoneName : "-");
                    DCell(t.Cell(), !string.IsNullOrWhiteSpace(o.DriverName) ? o.DriverName : "-", bold: true);
                    DCell(t.Cell(), $"{o.DeliveryFees:N2}", bold: true, color: (Color?)Colors.Teal.Darken3);
                    DCell(t.Cell(), $"{o.Bonus:N2}", bold: true, color: (Color?)Colors.Green.Darken3);
                    DCell(t.Cell(), $"{o.GrandTotal:N2}", bold: true);

                    row++;
                }
            });
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Delivery Fees & Driver Bonus" : "خدمة التوصيل وبونص الطيارين").FontSize(9).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(7.5f);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        int totalOrders = _orders.Count;
        decimal totalFees = _orders.Sum(o => o.DeliveryFees);
        decimal totalBonus = _orders.Sum(o => o.Bonus);

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Orders:" : "إجمالي طلبات التوصيل:").FontSize(8);
            r.RelativeItem().AlignRight().Text($"{totalOrders}").FontSize(8);
        });
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Fees:" : "إجمالي رسوم التوصيل:").Bold().FontSize(8);
            r.RelativeItem().AlignRight().Text($"{totalFees:N2} {Currency}").Bold().FontSize(8);
        });
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Bonus:" : "إجمالي البونص المستحق:").Bold().FontSize(8);
            r.RelativeItem().AlignRight().Text($"{totalBonus:N2} {Currency}").Bold().FontSize(8);
        });

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        col.Item().AlignCenter().Text(_isEnglish ? "Bonus Per Driver" : "توزيع البونص على الطيارين").Bold().FontSize(8);

        foreach (var d in _drivers.OrderByDescending(x => x.TotalBonus))
        {
            col.Item().PaddingVertical(1).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem(2).Text(d.DriverName).Bold().FontSize(8);
                    r.RelativeItem(1).AlignRight().Text($"بونص: {d.TotalBonus:N0}").Bold().FontSize(8);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text($"طلبات: {d.OrderCount}").FontSize(7);
                    r.RelativeItem().AlignRight().Text($"رسوم: {d.DeliveryFees:N0}").FontSize(7);
                });
            });
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        col.Item().AlignCenter().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
    }
}
