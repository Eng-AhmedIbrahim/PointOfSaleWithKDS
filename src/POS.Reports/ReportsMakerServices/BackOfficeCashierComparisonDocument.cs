using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

public class CashierComparisonItem
{
    public string CashierId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int CompletedCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal TotalDiscount { get; set; }
    public int DiscountCount { get; set; }
    public decimal TotalVoid { get; set; }
    public int VoidCount { get; set; }
    public decimal NetSales => TotalSales - TotalDiscount;
    public decimal AverageTicket => CompletedCount > 0 ? TotalSales / CompletedCount : 0;
    public decimal PercentageOfTotal { get; set; }
    public int TakeawayCount { get; set; }
    public int DeliveryCount { get; set; }
    public int DineInCount { get; set; }
    public bool IsTopSales { get; set; }
    public bool IsTopOrders { get; set; }
}

public class BackOfficeCashierComparisonDocument : IDocument
{
    private readonly List<CashierComparisonItem> _cashiers;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    private static readonly Color[] ChartPalette = {
        Colors.Purple.Medium, Colors.DeepPurple.Medium, Colors.Indigo.Medium,
        Colors.Blue.Medium, Colors.Teal.Medium, Colors.Cyan.Medium
    };

    public BackOfficeCashierComparisonDocument(
        List<CashierComparisonItem> cashiers,
        string branchName,
        DateTime fromDate,
        DateTime toDate,
        bool isThermal,
        string? language)
    {
        _cashiers   = cashiers ?? new List<CashierComparisonItem>();
        _branchName = branchName;
        _fromDate   = fromDate;
        _toDate     = toDate;
        _isThermal  = isThermal;
        _isEnglish  = (language ?? "ar").ToLower() == "en";
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
                    ComposeA4BarChart(col);
                    ComposeA4Table(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Purple.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Cashier Performance Comparison Report" : "تقرير مقارنة أداء وإنتاجية الكاشيرية")
                        .FontSize(10).FontColor(Colors.Purple.Lighten4);
                });
                row.ConstantItem(210).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"الفترة من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Purple.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Cashier Performance Comparison" : "مقارنة أداء الكاشيرية")
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
        int totalCashiers = _cashiers.Count;
        int totalOrders = _cashiers.Sum(c => c.OrderCount);
        decimal totalRevenue = _cashiers.Sum(c => c.TotalSales);
        var topSales = _cashiers.OrderByDescending(c => c.TotalSales).FirstOrDefault(c => c.TotalSales > 0);
        decimal totalDiscounts = _cashiers.Sum(c => c.TotalDiscount);
        decimal totalVoids = _cashiers.Sum(c => c.TotalVoid);

        col.Item().PaddingBottom(6).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Cashiers" : "عدد الكاشيرية",
                $"{totalCashiers}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Orders" : "إجمالي الطلبات",
                $"{totalOrders:N0}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Sales" : "إجمالي المبيعات",
                $"{totalRevenue:N2} {Currency}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(1.3f), _isEnglish ? "Top Cashier (Sales)" : "أعلى كاشير مبيعاً 🏆",
                topSales != null ? $"{topSales.CashierName} ({topSales.TotalSales:N0} {Currency})" : "-",
                Colors.Amber.Darken4, Colors.Amber.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Discounts / Voids" : "الخصومات / الإلغاءات",
                $"{totalDiscounts:N0} / {totalVoids:N0} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5);
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

    private void ComposeA4BarChart(ColumnDescriptor col)
    {
        if (!_cashiers.Any()) return;
        decimal maxSales = _cashiers.Max(c => c.TotalSales);

        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Cashier Revenue Comparison Chart" : "الرسم البياني لمقارنة مبيعات الكاشيرية")
                .FontSize(10).Bold().FontColor(Colors.Purple.Darken3);

            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(120);  // Cashier name
                    cd.RelativeColumn();     // Bar
                    cd.ConstantColumn(95);   // Sales
                    cd.ConstantColumn(65);   // Orders
                    cd.ConstantColumn(45);   // Share %
                });

                int idx = 0;
                foreach (var c in _cashiers.OrderByDescending(x => x.TotalSales))
                {
                    float pct = maxSales > 0 ? (float)(c.TotalSales / maxSales) : 0;
                    bool isTop = c.IsTopSales;
                    var barColor = isTop ? Colors.Amber.Darken2 : ChartPalette[idx % ChartPalette.Length];
                    idx++;

                    t.Cell().Padding(2).AlignRight().Text(txt =>
                    {
                        if (isTop) txt.Span("★ ").FontColor(Colors.Amber.Darken3).Bold().FontSize(8);
                        txt.Span(c.CashierName).Bold().FontColor(isTop ? Colors.Amber.Darken4 : Colors.Black).FontSize(8);
                    });

                    t.Cell().Padding(2).Column(cell =>
                    {
                        cell.Item().Height(12).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, pct)).Background(barColor).Height(9);
                            r.RelativeItem(Math.Max(0.01f, 1 - pct)).Height(9);
                        });
                    });

                    t.Cell().Padding(2).AlignRight()
                        .Text($"{c.TotalSales:N2} {Currency}").FontSize(8).Bold()
                        .FontColor(isTop ? Colors.Amber.Darken4 : Colors.Black);

                    t.Cell().Padding(2).AlignCenter()
                        .Text($"{c.OrderCount} {(_isEnglish ? "orders" : "طلب")}").FontSize(7.5f);

                    t.Cell().Padding(2).AlignCenter()
                        .Text($"{c.PercentageOfTotal:F1}%").FontSize(7.5f).Bold()
                        .FontColor(isTop ? Colors.Amber.Darken3 : Colors.Grey.Darken2);
                }
            });
        });
    }

    private void ComposeA4Table(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(22);   // #
                cd.RelativeColumn(2f);   // Cashier
                cd.RelativeColumn(0.9f); // Orders
                cd.RelativeColumn(1.3f); // Sales
                cd.RelativeColumn(1.1f); // Cash
                cd.RelativeColumn(1.1f); // Credit
                cd.RelativeColumn(1f);   // Discount
                cd.RelativeColumn(1f);   // Void
                cd.RelativeColumn(1.1f); // Avg Ticket
                cd.RelativeColumn(0.9f); // % Share
                cd.RelativeColumn(1f);   // Rank/Badge
            });

            void HCell(IContainer c, string text)
            {
                c.Background(Colors.Purple.Darken3).Padding(4).AlignCenter().Text(text)
                    .FontSize(7.5f).Bold().FontColor(Colors.White);
            }

            t.Header(h =>
            {
                h.Cell().Element(c => HCell(c, "#"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Cashier Name" : "اسم الكاشير"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Orders" : "الطلبات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Total Sales" : "إجمالي المبيعات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Cash" : "نقدي"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Visa/Credit" : "فيزا"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Discounts" : "الخصومات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Voids" : "الإلغاءات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Avg / Order" : "متوسط الطلب"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "% Share" : "النسبة"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Rank" : "الترتيب"));
            });

            int rowIdx = 1;
            foreach (var c in _cashiers.OrderByDescending(x => x.TotalSales))
            {
                var bg = c.IsTopSales ? Colors.Amber.Lighten5 : (rowIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White);

                void DCell(IContainer cell, string text, bool bold = false, Color? color = null)
                {
                    var txt = cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(3).AlignCenter().Text(text ?? "").FontSize(7.5f);
                    if (bold) txt.Bold();
                    if (color.HasValue) txt.FontColor(color.Value);
                }

                DCell(t.Cell(), rowIdx.ToString());
                DCell(t.Cell(), c.CashierName ?? "", bold: true, color: c.IsTopSales ? (Color?)Colors.Amber.Darken4 : null);
                DCell(t.Cell(), $"{c.OrderCount:N0}");
                DCell(t.Cell(), $"{c.TotalSales:N2}", bold: true, color: (Color?)Colors.Purple.Darken3);
                DCell(t.Cell(), $"{c.CashAmount:N2}");
                DCell(t.Cell(), $"{c.CreditAmount:N2}");
                DCell(t.Cell(), c.TotalDiscount > 0 ? $"{c.TotalDiscount:N1} ({c.DiscountCount})" : "-", color: c.TotalDiscount > 0 ? (Color?)Colors.Orange.Darken3 : null);
                DCell(t.Cell(), c.TotalVoid > 0 ? $"{c.TotalVoid:N1} ({c.VoidCount})" : "-", color: c.TotalVoid > 0 ? (Color?)Colors.Red.Darken3 : null);
                DCell(t.Cell(), $"{c.AverageTicket:N1}");
                DCell(t.Cell(), $"{c.PercentageOfTotal:F1}%", bold: true);
                DCell(t.Cell(), c.IsTopSales ? "الأعلى مبيعات 🏆" : $"#{rowIdx}", bold: c.IsTopSales, color: c.IsTopSales ? (Color?)Colors.Amber.Darken3 : (Color?)Colors.Grey.Darken1);

                rowIdx++;
            }

            // Totals Row
            void TCell(IContainer cell, string text)
            {
                cell.Background(Colors.Purple.Lighten5).BorderTop(1.5f).BorderColor(Colors.Purple.Darken2)
                    .Padding(4).AlignCenter().Text(text).FontSize(8).Bold().FontColor(Colors.Purple.Darken4);
            }

            int grandOrders = _cashiers.Sum(x => x.OrderCount);
            decimal grandSales = _cashiers.Sum(x => x.TotalSales);
            decimal grandCash = _cashiers.Sum(x => x.CashAmount);
            decimal grandCredit = _cashiers.Sum(x => x.CreditAmount);
            decimal grandDiscounts = _cashiers.Sum(x => x.TotalDiscount);
            decimal grandVoids = _cashiers.Sum(x => x.TotalVoid);
            decimal grandAvg = grandOrders > 0 ? grandSales / grandOrders : 0;

            t.Cell().Element(c => TCell(c, ""));
            t.Cell().Element(c => TCell(c, _isEnglish ? "Total" : "الإجمالي"));
            t.Cell().Element(c => TCell(c, grandOrders.ToString("N0")));
            t.Cell().Element(c => TCell(c, $"{grandSales:N2} {Currency}"));
            t.Cell().Element(c => TCell(c, $"{grandCash:N2}"));
            t.Cell().Element(c => TCell(c, $"{grandCredit:N2}"));
            t.Cell().Element(c => TCell(c, $"{grandDiscounts:N1}"));
            t.Cell().Element(c => TCell(c, $"{grandVoids:N1}"));
            t.Cell().Element(c => TCell(c, $"{grandAvg:N1}"));
            t.Cell().Element(c => TCell(c, "100%"));
            t.Cell().Element(c => TCell(c, "-"));
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Cashier Performance Comparison" : "مقارنة أداء الكاشيرية").FontSize(9).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(7.5f);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        int grandOrders = _cashiers.Sum(x => x.OrderCount);
        decimal grandSales = _cashiers.Sum(x => x.TotalSales);
        var top = _cashiers.OrderByDescending(c => c.TotalSales).FirstOrDefault();

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Sales:" : "إجمالي المبيعات:").Bold().FontSize(8);
            r.RelativeItem().AlignRight().Text($"{grandSales:N2} {Currency}").Bold().FontSize(8);
        });
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Orders:" : "إجمالي الطلبات:").FontSize(7.5f);
            r.RelativeItem().AlignRight().Text($"{grandOrders}").FontSize(7.5f);
        });

        if (top != null)
        {
            col.Item().Background(Colors.Grey.Lighten4).Padding(2).Row(r =>
            {
                r.RelativeItem().Text(_isEnglish ? "Top Cashier:" : "أعلى كاشير:").Bold().FontSize(8);
                r.RelativeItem().AlignRight().Text($"{top.CashierName} ({top.TotalSales:N0})").Bold().FontSize(8);
            });
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

        foreach (var c in _cashiers.OrderByDescending(x => x.TotalSales))
        {
            col.Item().PaddingVertical(1).Column(colItem =>
            {
                colItem.Item().Row(r =>
                {
                    r.RelativeItem(2).Text(c.CashierName).Bold().FontSize(8);
                    r.RelativeItem(1).AlignRight().Text($"{c.TotalSales:N2}").Bold().FontSize(8);
                });
                colItem.Item().Row(r =>
                {
                    r.RelativeItem().Text($"طلبات: {c.OrderCount} ({c.PercentageOfTotal:F1}%)").FontSize(7);
                    r.RelativeItem().AlignRight().Text($"نقدي: {c.CashAmount:N0} | فيزا: {c.CreditAmount:N0}").FontSize(7);
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
