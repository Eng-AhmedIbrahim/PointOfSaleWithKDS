using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

public class BranchComparisonItem
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AverageOrderValue => OrderCount > 0 ? TotalSales / OrderCount : 0;
    public decimal PercentageOfTotal { get; set; }
    public int DineInCount { get; set; }
    public decimal DineInTotal { get; set; }
    public int TakeawayCount { get; set; }
    public decimal TakeawayTotal { get; set; }
    public int DeliveryCount { get; set; }
    public decimal DeliveryTotal { get; set; }
    public bool IsTopBranch { get; set; }
}

public class BackOfficeBranchComparisonDocument : IDocument
{
    private readonly List<BranchComparisonItem> _branches;
    private readonly string _companyOrBranchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    private static readonly Color[] ChartPalette = {
        Colors.Indigo.Medium, Colors.Blue.Medium, Colors.Cyan.Medium,
        Colors.Teal.Medium, Colors.Purple.Medium, Colors.DeepPurple.Medium
    };

    public BackOfficeBranchComparisonDocument(
        List<BranchComparisonItem> branches,
        string companyOrBranchName,
        DateTime fromDate,
        DateTime toDate,
        bool isThermal,
        string? language)
    {
        _branches = branches ?? new List<BranchComparisonItem>();
        _companyOrBranchName = companyOrBranchName;
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
            col.Item().Background(Colors.Indigo.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_companyOrBranchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Central Call Center - Branch Comparison Report" : "الكول سنتر المركزي - تقرير مقارنة مبيعات وأداء الفروع")
                        .FontSize(10).FontColor(Colors.Indigo.Lighten4);
                });
                row.ConstantItem(210).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"الفترة من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Indigo.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Central Call Center - Branch Comparison" : "الكول سنتر المركزي - مقارنة الفروع")
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
        int totalBranches = _branches.Count;
        int totalOrders = _branches.Sum(b => b.OrderCount);
        decimal totalRevenue = _branches.Sum(b => b.TotalSales);
        var topBranch = _branches.OrderByDescending(b => b.TotalSales).FirstOrDefault(b => b.TotalSales > 0);
        decimal avgPerBranch = totalBranches > 0 ? totalRevenue / totalBranches : 0;

        col.Item().PaddingBottom(6).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Branches" : "عدد الفروع",
                $"{totalBranches}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Orders" : "إجمالي الطلبات",
                $"{totalOrders:N0}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Sales" : "إجمالي المبيعات",
                $"{totalRevenue:N2} {Currency}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(1.4f), _isEnglish ? "Top Performing Branch" : "أعلى فرع مبيعاً 🏆",
                topBranch != null ? $"{topBranch.BranchName} ({topBranch.TotalSales:N0} {Currency})" : "-",
                Colors.Amber.Darken4, Colors.Amber.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg Sales / Branch" : "متوسط مبيعات الفرع",
                $"{avgPerBranch:N2} {Currency}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
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
        if (!_branches.Any()) return;
        decimal maxAmount = _branches.Max(b => b.TotalSales);

        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Branch Revenue Comparison Chart" : "الرسم البياني لمقارنة مبيعات الفروع")
                .FontSize(10).Bold().FontColor(Colors.Indigo.Darken3);

            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(120);  // Branch name
                    cd.RelativeColumn();     // Bar chart
                    cd.ConstantColumn(95);   // Sales Amount
                    cd.ConstantColumn(65);   // Orders
                    cd.ConstantColumn(45);   // % share
                });

                int idx = 0;
                foreach (var b in _branches.OrderByDescending(x => x.TotalSales))
                {
                    float pct = maxAmount > 0 ? (float)(b.TotalSales / maxAmount) : 0;
                    bool isTop = b.IsTopBranch;
                    var barColor = isTop ? Colors.Amber.Darken2 : ChartPalette[idx % ChartPalette.Length];
                    idx++;

                    t.Cell().Padding(2).AlignRight().Text(txt =>
                    {
                        if (isTop) txt.Span("★ ").FontColor(Colors.Amber.Darken3).Bold().FontSize(8);
                        txt.Span(b.BranchName).Bold().FontColor(isTop ? Colors.Amber.Darken4 : Colors.Black).FontSize(8);
                    });

                    t.Cell().Padding(2).Column(c =>
                    {
                        c.Item().Height(12).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, pct)).Background(barColor).Height(9);
                            r.RelativeItem(Math.Max(0.01f, 1 - pct)).Height(9);
                        });
                    });

                    t.Cell().Padding(2).AlignRight()
                        .Text($"{b.TotalSales:N2} {Currency}").FontSize(8).Bold()
                        .FontColor(isTop ? Colors.Amber.Darken4 : Colors.Black);

                    t.Cell().Padding(2).AlignCenter()
                        .Text($"{b.OrderCount} {(_isEnglish ? "orders" : "طلب")}").FontSize(7.5f);

                    t.Cell().Padding(2).AlignCenter()
                        .Text($"{b.PercentageOfTotal:F1}%").FontSize(7.5f).Bold()
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
                cd.ConstantColumn(24);   // #
                cd.RelativeColumn(2f);   // Branch
                cd.RelativeColumn(1f);   // Orders
                cd.RelativeColumn(1.3f); // Total Sales
                cd.RelativeColumn(1.1f); // Avg Order
                cd.RelativeColumn(0.9f); // % Share
                cd.RelativeColumn(1f);   // Takeaway
                cd.RelativeColumn(1f);   // Delivery
                cd.RelativeColumn(1f);   // DineIn
                cd.RelativeColumn(1f);   // Rank/Status
            });

            void HCell(IContainer c, string text)
            {
                c.Background(Colors.Indigo.Darken3).Padding(4).AlignCenter().Text(text)
                    .FontSize(7.5f).Bold().FontColor(Colors.White);
            }

            t.Header(h =>
            {
                h.Cell().Element(c => HCell(c, "#"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Branch Name" : "اسم الفرع"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Orders" : "عدد الطلبات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Total Sales" : "إجمالي المبيعات"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Avg / Order" : "متوسط الطلب"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "% Share" : "النسبة"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "TakeAway" : "تيك أواي"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Delivery" : "توصيل"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "DineIn" : "صالة"));
                h.Cell().Element(c => HCell(c, _isEnglish ? "Status" : "الترتيب"));
            });

            int rowIdx = 1;
            foreach (var b in _branches.OrderByDescending(x => x.TotalSales))
            {
                var bg = b.IsTopBranch ? Colors.Amber.Lighten5 : (rowIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White);

                void DCell(IContainer c, string text, bool bold = false, Color? color = null)
                {
                    var txt = c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(3).AlignCenter().Text(text ?? "").FontSize(7.5f);
                    if (bold) txt.Bold();
                    if (color.HasValue) txt.FontColor(color.Value);
                }

                DCell(t.Cell(), rowIdx.ToString());
                DCell(t.Cell(), b.BranchName ?? "", bold: true, color: b.IsTopBranch ? (Color?)Colors.Amber.Darken4 : null);
                DCell(t.Cell(), b.OrderCount.ToString("N0"));
                DCell(t.Cell(), $"{b.TotalSales:N2}", bold: true, color: (Color?)Colors.Indigo.Darken3);
                DCell(t.Cell(), $"{b.AverageOrderValue:N1}");
                DCell(t.Cell(), $"{b.PercentageOfTotal:F1}%", bold: true);
                DCell(t.Cell(), $"{b.TakeawayTotal:N0} ({b.TakeawayCount})");
                DCell(t.Cell(), $"{b.DeliveryTotal:N0} ({b.DeliveryCount})");
                DCell(t.Cell(), $"{b.DineInTotal:N0} ({b.DineInCount})");
                DCell(t.Cell(), b.IsTopBranch ? "الأعلى 🏆" : $"#{rowIdx}", bold: b.IsTopBranch, color: b.IsTopBranch ? (Color?)Colors.Amber.Darken3 : (Color?)Colors.Grey.Darken1);

                rowIdx++;
            }

            // Totals Row
            void TCell(IContainer c, string text, bool bold = true)
            {
                c.Background(Colors.Indigo.Lighten5).BorderTop(1.5f).BorderColor(Colors.Indigo.Darken2)
                    .Padding(4).AlignCenter().Text(text).FontSize(8).Bold().FontColor(Colors.Indigo.Darken4);
            }

            int grandOrders = _branches.Sum(x => x.OrderCount);
            decimal grandSales = _branches.Sum(x => x.TotalSales);
            decimal grandAvg = grandOrders > 0 ? grandSales / grandOrders : 0;
            decimal grandTake = _branches.Sum(x => x.TakeawayTotal);
            decimal grandDeliv = _branches.Sum(x => x.DeliveryTotal);
            decimal grandDine = _branches.Sum(x => x.DineInTotal);

            t.Cell().Element(c => TCell(c, ""));
            t.Cell().Element(c => TCell(c, _isEnglish ? "Total All Branches" : "الإجمالي العام"));
            t.Cell().Element(c => TCell(c, grandOrders.ToString("N0")));
            t.Cell().Element(c => TCell(c, $"{grandSales:N2} {Currency}"));
            t.Cell().Element(c => TCell(c, $"{grandAvg:N1}"));
            t.Cell().Element(c => TCell(c, "100%"));
            t.Cell().Element(c => TCell(c, $"{grandTake:N0}"));
            t.Cell().Element(c => TCell(c, $"{grandDeliv:N0}"));
            t.Cell().Element(c => TCell(c, $"{grandDine:N0}"));
            t.Cell().Element(c => TCell(c, "-"));
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_companyOrBranchName).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Central Call Center" : "الكول سنتر المركزي").FontSize(8);
        col.Item().AlignCenter().Text(_isEnglish ? "Branch Sales Comparison" : "مقارنة مبيعات الفروع").FontSize(9).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(7.5f);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        int grandOrders = _branches.Sum(x => x.OrderCount);
        decimal grandSales = _branches.Sum(x => x.TotalSales);
        var top = _branches.OrderByDescending(b => b.TotalSales).FirstOrDefault();

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
                r.RelativeItem().Text(_isEnglish ? "Top Branch:" : "أعلى فرع:").Bold().FontSize(8);
                r.RelativeItem().AlignRight().Text($"{top.BranchName} ({top.TotalSales:N0})").Bold().FontSize(8);
            });
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

        foreach (var b in _branches.OrderByDescending(x => x.TotalSales))
        {
            col.Item().PaddingVertical(1).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem(2).Text(b.BranchName).Bold().FontSize(8);
                    r.RelativeItem(1).AlignRight().Text($"{b.TotalSales:N2}").Bold().FontSize(8);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text($"طلبات: {b.OrderCount} ({b.PercentageOfTotal:F1}%)").FontSize(7);
                    r.RelativeItem().AlignRight().Text($"متوسط: {b.AverageOrderValue:N0}").FontSize(7);
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
