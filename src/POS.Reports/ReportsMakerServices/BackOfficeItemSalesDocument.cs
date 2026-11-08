using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeItemSalesDocument : IDocument
{
    private readonly List<SalesItemSummaryDto> _items;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _reportTitle;
    private readonly bool _isThermal;
    private readonly bool _isEnglish;
    private const string Currency = "ج.م";

    public BackOfficeItemSalesDocument(List<SalesItemSummaryDto> items, string branchName, DateTime fromDate, DateTime toDate, string reportTitle = "تقرير مبيعات الأصناف", string lang = "ar", bool isThermal = false)
    {
        _items       = items ?? new List<SalesItemSummaryDto>();
        _branchName  = branchName;
        _fromDate    = fromDate;
        _toDate      = toDate;
        _reportTitle = reportTitle;
        _isEnglish   = (lang ?? "ar").ToLower() == "en";
        _isThermal   = isThermal;
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
                    ComposeA4ItemsTable(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Indigo.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_reportTitle).FontSize(10).FontColor(Colors.Indigo.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Indigo.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).Row(row =>
            {
                row.RelativeItem().Text(_reportTitle)
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
        decimal totalRevenue = _items.Sum(x => x.TotalAmount);
        decimal totalQty = _items.Sum(x => x.Quantity);
        int uniqueItems = _items.Count;
        int uniqueCategories = _items.Select(x => x.CategoryName).Distinct().Count();
        var topItem = _items.OrderByDescending(x => x.TotalAmount).FirstOrDefault();

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي الإيراد",
                $"{totalRevenue:0.##} {Currency}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Qty" : "إجمالي الكميات",
                $"{totalQty:0.##}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Items" : "عدد الأصناف",
                $"{uniqueItems}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Categories" : "الفئات",
                $"{uniqueCategories}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Top Item" : "أعلى صنف",
                topItem?.ItemName ?? "-", Colors.Teal.Darken4, Colors.Teal.Lighten5);
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
        var categories = _items.GroupBy(i => string.IsNullOrWhiteSpace(i.CategoryName) ? "Other" : i.CategoryName)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Total).Take(10).ToList();
        if (!categories.Any()) return;

        decimal maxVal = categories.Max(c => c.Total);

        col.Item().PaddingBottom(10).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Sales by Category" : "المبيعات حسب الفئة")
                .FontSize(11).Bold().FontColor(Colors.Indigo.Darken3);
            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(130);  // Category name
                    cd.RelativeColumn();     // Bar
                    cd.ConstantColumn(80);   // Amount
                });

                Color[] barColors = {
                    Colors.Blue.Medium, Colors.Indigo.Medium, Colors.Cyan.Medium,
                    Colors.Teal.Medium, Colors.Green.Medium, Colors.Amber.Medium,
                    Colors.Orange.Medium, Colors.Purple.Medium
                };

                for (int i = 0; i < categories.Count; i++)
                {
                    var cat = categories[i];
                    float pct = maxVal > 0 ? (float)(cat.Total / maxVal) : 0;
                    var barColor = barColors[i % barColors.Length];

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
                }
            });
        });
    }

    private void ComposeA4ItemsTable(ColumnDescriptor col)
    {
        var ranked = _items.OrderByDescending(x => x.TotalAmount).ToList();
        decimal grandTotal = ranked.Sum(x => x.TotalAmount);

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);  // Rank
                cd.RelativeColumn(4);   // Name
                cd.RelativeColumn(2);   // Category
                cd.RelativeColumn(1.5f);// Qty
                cd.RelativeColumn(1.5f);// Unit Price
                cd.RelativeColumn(2);   // Total
                cd.RelativeColumn(1.5f);// %
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Indigo.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);
                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Item" : "الصنف");
                Hdr(h.Cell(), _isEnglish ? "Category" : "الفئة");
                Hdr(h.Cell(), _isEnglish ? "Qty" : "الكمية");
                Hdr(h.Cell(), _isEnglish ? "Unit Price" : "سعر الوحدة");
                Hdr(h.Cell(), _isEnglish ? "Total" : "الإجمالي");
                Hdr(h.Cell(), _isEnglish ? "%" : "النسبة");
            });

            for (int i = 0; i < ranked.Count; i++)
            {
                var item = ranked[i];
                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                decimal pct = grandTotal > 0 ? item.TotalAmount / grandTotal * 100 : 0;

                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{i + 1}").FontSize(9).Bold().FontColor(Colors.Indigo.Darken2);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignRight().Text(item.ItemName).FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text(item.CategoryName).FontSize(8);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text(item.Quantity.ToString("0.##")).FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{item.UnitPrice:0.##}").FontSize(9);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{item.TotalAmount:0.##} {Currency}").FontSize(9).Bold();
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(4).AlignCenter().Text($"{pct:0.0}%").FontSize(8).FontColor(Colors.Indigo.Darken2);
            }

            // Totals
            t.Cell().ColumnSpan(5).Background(Colors.Indigo.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Indigo.Darken4).Padding(6).AlignCenter()
                .Text($"{grandTotal:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Indigo.Darken4).Padding(6).AlignCenter()
                .Text("100%").FontSize(10).Bold().FontColor(Colors.White);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_reportTitle).FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        var ranked = _items.OrderByDescending(x => x.TotalAmount).ToList();
        decimal grandTotal = ranked.Sum(x => x.TotalAmount);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Items:" : "الأصناف:").FontSize(8);
            row.ConstantItem(60).AlignRight().Text(ranked.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Revenue:" : "الإجمالي:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{grandTotal:0.##} {Currency}").FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        int rank = 1;
        foreach (var item in ranked)
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(16).Text($"{rank}.").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                row.RelativeItem().Text(item.ItemName).FontSize(8);
                row.ConstantItem(70).AlignRight().Text($"{item.TotalAmount:0.##}").FontSize(8).Bold();
            });
            rank++;
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
