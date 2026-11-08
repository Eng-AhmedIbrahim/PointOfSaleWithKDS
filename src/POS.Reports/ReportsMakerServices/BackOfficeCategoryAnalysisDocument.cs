using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeCategoryAnalysisDocument : IDocument
{
    private readonly List<SalesItemSummaryDto> _items;
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    private static readonly Color[] BarColors =
    {
        Colors.Purple.Medium, Colors.Indigo.Medium, Colors.Blue.Medium,
        Colors.Teal.Medium, Colors.Green.Medium, Colors.Amber.Medium,
        Colors.Orange.Medium, Colors.DeepOrange.Medium
    };

    public BackOfficeCategoryAnalysisDocument(List<SalesItemSummaryDto> items, List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, bool isThermal = false, string lang = "ar")
    {
        _items      = items ?? new List<SalesItemSummaryDto>();
        _orders     = orders ?? new List<OrderDto>();
        _branchName = branchName;
        _fromDate   = fromDate;
        _toDate     = toDate;
        _isThermal  = isThermal;
        _isEnglish  = (lang ?? "ar").ToLower() == "en";
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
            if (!_isEnglish) page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 2 : 4);
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

    private List<(string Name, decimal Total, int ItemCount, int OrderCount)> GetCategoryData()
    {
        return _items.GroupBy(i => string.IsNullOrWhiteSpace(i.CategoryName) ? (_isEnglish ? "Uncategorized" : "غير محدد") : i.CategoryName)
            .Select(g => (
                Name: g.Key,
                Total: g.Sum(x => x.TotalAmount),
                ItemCount: g.Count(),
                OrderCount: _orders.Count(o => o.OrderDetails?.Any(d =>
                    (d.CategoryName ?? "") == g.Key && d.IsVoided != true) ?? false)
            ))
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Purple.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Category Analysis Report" : "تقرير تحليل الفئات").FontSize(10).FontColor(Colors.Purple.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
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
            row.RelativeItem().Text(_isEnglish ? "Category Analysis Report" : "تقرير تحليل الفئات")
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
        var categories = GetCategoryData();
        decimal grandTotal = categories.Sum(x => x.Total);
        int catCount = categories.Count;
        var top = categories.FirstOrDefault();
        decimal topPct = grandTotal > 0 && top.Total > 0 ? top.Total / grandTotal * 100 : 0;

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي الإيراد",
                $"{grandTotal:0.##} {Currency}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Categories" : "عدد الفئات",
                $"{catCount}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Top Category" : "أعلى فئة",
                top.Name ?? "-", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Top Category %" : "نسبة أعلى فئة",
                $"{topPct:0.0}%", Colors.Orange.Darken4, Colors.Orange.Lighten5);
        });
    }

    private static void KpiCard(IContainer c, string label, string value, Color textColor, Color bgColor)
    {
        c.Background(bgColor).Border(1).BorderColor(textColor).Padding(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(textColor);
            col.Item().Text(value).FontSize(10).Bold().FontColor(textColor);
        });
    }

    private void ComposeA4BarChart(ColumnDescriptor col)
    {
        var categories = GetCategoryData();
        if (!categories.Any()) return;
        decimal maxVal = categories.Max(c => c.Total);

        col.Item().PaddingBottom(10).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Revenue by Category" : "الإيراد حسب الفئة")
                .FontSize(11).Bold().FontColor(Colors.Purple.Darken3);
            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(140);
                    cd.RelativeColumn();
                    cd.ConstantColumn(90);
                    cd.ConstantColumn(50);
                });

                for (int i = 0; i < categories.Count; i++)
                {
                    var cat = categories[i];
                    float pct = maxVal > 0 ? (float)(cat.Total / maxVal) : 0;
                    var barColor = BarColors[i % BarColors.Length];

                    t.Cell().Padding(2).AlignRight().Text(cat.Name).FontSize(9);
                    t.Cell().Padding(2).Column(c =>
                    {
                        c.Item().Height(14).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, pct)).Background(barColor).Height(10);
                            r.RelativeItem(Math.Max(0.01f, 1 - pct)).Height(10);
                        });
                    });
                    t.Cell().Padding(2).AlignRight().Text($"{cat.Total:0.##} {Currency}").FontSize(9).Bold();
                    decimal grandTotal2 = categories.Sum(x => x.Total);
                    string pctStr = grandTotal2 > 0 ? $"{cat.Total / grandTotal2 * 100:0.0}%" : "0%";
                    t.Cell().Padding(2).AlignCenter().Text(pctStr).FontSize(9).FontColor(Colors.Purple.Darken2);
                }
            });
        });
    }

    private void ComposeA4Table(ColumnDescriptor col)
    {
        var categories = GetCategoryData();
        decimal grandTotal = categories.Sum(x => x.Total);

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // Rank
                cd.RelativeColumn(3);    // Category
                cd.RelativeColumn(1.5f); // Items
                cd.RelativeColumn(1.5f); // Orders
                cd.RelativeColumn(2);    // Total
                cd.RelativeColumn(1.5f); // %
                cd.RelativeColumn(3);    // Visual bar
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Purple.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);
                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Category" : "الفئة");
                Hdr(h.Cell(), _isEnglish ? "Items" : "الأصناف");
                Hdr(h.Cell(), _isEnglish ? "Orders" : "الطلبات");
                Hdr(h.Cell(), _isEnglish ? "Total" : "الإجمالي");
                Hdr(h.Cell(), _isEnglish ? "%" : "النسبة");
                Hdr(h.Cell(), _isEnglish ? "Share" : "الحصة");
            });

            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                decimal pct = grandTotal > 0 ? cat.Total / grandTotal * 100 : 0;
                float barPct = grandTotal > 0 ? (float)(cat.Total / grandTotal) : 0;
                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                var barColor = BarColors[i % BarColors.Length];

                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{i + 1}").FontSize(9).Bold().FontColor(Colors.Purple.Darken2);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignRight().Text(cat.Name).FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text(cat.ItemCount.ToString()).FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text(cat.OrderCount.ToString()).FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{cat.Total:0.##} {Currency}").FontSize(9).Bold();
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{pct:0.1}%").FontSize(9).FontColor(Colors.Purple.Darken2);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).Column(c =>
                    {
                        c.Item().Height(10).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, barPct)).Background(barColor).Height(8);
                            r.RelativeItem(Math.Max(0.01f, 1 - barPct)).Height(8);
                        });
                    });
            }

            t.Cell().ColumnSpan(4).Background(Colors.Purple.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Purple.Darken4).Padding(6).AlignCenter()
                .Text($"{grandTotal:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Purple.Darken4).Padding(6).AlignCenter()
                .Text("100%").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Purple.Darken4).Padding(6);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Category Analysis" : "تحليل الفئات").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        var categories = GetCategoryData();
        decimal grandTotal = categories.Sum(x => x.Total);

        foreach (var cat in categories)
        {
            decimal pct = grandTotal > 0 ? cat.Total / grandTotal * 100 : 0;
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(cat.Name).FontSize(8);
                row.ConstantItem(45).AlignCenter().Text($"{pct:0.0}%").FontSize(8).FontColor(Colors.Purple.Darken1);
                row.ConstantItem(55).AlignRight().Text($"{cat.Total:0.##}").FontSize(8).Bold();
            });
        }
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "TOTAL" : "الإجمالي").FontSize(9).Bold();
            row.ConstantItem(70).AlignRight().Text($"{grandTotal:0.##} {Currency}").FontSize(9).Bold();
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
