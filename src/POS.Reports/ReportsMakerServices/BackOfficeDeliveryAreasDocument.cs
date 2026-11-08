using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class DeliveryAreaReportItem
{
    public string ZoneName { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageOrderValue => TotalOrders > 0 ? TotalAmount / TotalOrders : 0;
}


public class BackOfficeDeliveryAreasDocument : IDocument
{
    private readonly List<DeliveryAreaReportItem> _areas;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    private static readonly Color[] BarColors = {
        Colors.Orange.Medium, Colors.DeepOrange.Medium, Colors.Amber.Medium,
        Colors.Teal.Medium, Colors.Blue.Medium, Colors.Indigo.Medium
    };

    public BackOfficeDeliveryAreasDocument(
        List<DeliveryAreaReportItem> areas,
        string branchName,
        DateTime fromDate,
        DateTime toDate,
        bool isThermal,
        string? language)
    {
        _areas      = areas ?? new List<DeliveryAreaReportItem>();
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

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Orange.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Top Delivery Areas Report" : "تقرير مناطق التوصيل الأكثر طلباً").FontSize(10).FontColor(Colors.Orange.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Orange.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Delivery Areas Report" : "تقرير مناطق التوصيل")
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
        int totalZones = _areas.Count;
        int totalOrders = _areas.Sum(a => a.TotalOrders);
        decimal totalRevenue = _areas.Sum(a => a.TotalAmount);
        decimal avgOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        var topArea = _areas.OrderByDescending(a => a.TotalAmount).FirstOrDefault();

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي الإيراد",
                $"{totalRevenue:0.##} {Currency}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Orders" : "إجمالي الطلبات",
                $"{totalOrders}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Delivery Zones" : "عدد المناطق",
                $"{totalZones}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg / Order" : "متوسط الطلب",
                $"{avgOrder:0.##} {Currency}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Top Zone" : "أعلى منطقة",
                topArea?.ZoneName ?? "-", Colors.DeepOrange.Darken4, Colors.DeepOrange.Lighten5);
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
        if (!_areas.Any()) return;
        decimal maxVal = _areas.Max(a => a.TotalAmount);

        col.Item().PaddingBottom(10).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Revenue by Delivery Zone" : "الإيراد حسب منطقة التوصيل")
                .FontSize(11).Bold().FontColor(Colors.Orange.Darken3);
            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(140);
                    cd.RelativeColumn();
                    cd.ConstantColumn(90);
                    cd.ConstantColumn(50);
                });

                for (int i = 0; i < Math.Min(10, _areas.Count); i++)
                {
                    var area = _areas[i];
                    float pct = maxVal > 0 ? (float)(area.TotalAmount / maxVal) : 0;
                    var barColor = BarColors[i % BarColors.Length];

                    t.Cell().Padding(2).AlignRight().Text(area.ZoneName ?? "-").FontSize(9);
                    t.Cell().Padding(2).Column(c =>
                    {
                        c.Item().Height(14).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, pct)).Background(barColor).Height(10);
                            r.RelativeItem(Math.Max(0.01f, 1 - pct)).Height(10);
                        });
                    });
                    t.Cell().Padding(2).AlignRight().Text($"{area.TotalAmount:0.##} {Currency}").FontSize(9).Bold();
                    t.Cell().Padding(2).AlignCenter().Text($"{area.TotalOrders} {(_isEnglish ? "ord" : "طلب")}").FontSize(8);
                }
            });
        });
    }

    private void ComposeA4Table(ColumnDescriptor col)
    {
        var ranked = _areas.OrderByDescending(a => a.TotalAmount).ToList();
        decimal grandTotal = ranked.Sum(a => a.TotalAmount);

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // Rank
                cd.RelativeColumn(3);    // Zone
                cd.RelativeColumn(1.5f); // Orders
                cd.RelativeColumn(2);    // Avg / Order
                cd.RelativeColumn(2);    // Revenue
                cd.RelativeColumn(1.5f); // % Share
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Orange.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Delivery Zone" : "منطقة التوصيل");
                Hdr(h.Cell(), _isEnglish ? "Orders" : "الطلبات");
                Hdr(h.Cell(), _isEnglish ? "Avg / Order" : "متوسط الطلب");
                Hdr(h.Cell(), _isEnglish ? "Revenue" : "الإيراد");
                Hdr(h.Cell(), _isEnglish ? "Share %" : "النسبة");
            });

            for (int i = 0; i < ranked.Count; i++)
            {
                var area = ranked[i];
                decimal pct = grandTotal > 0 ? (area.TotalAmount / grandTotal * 100) : 0;
                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                void DataCell(IContainer cell, string text, bool bold = false)
                {
                    var txt = cell.Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).AlignCenter().Text(text).FontSize(9);
                    if (bold) txt.Bold();
                }

                void DataCellColored(IContainer cell, string text, Color clr, bool bold = false)
                {
                    var txt = cell.Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).AlignCenter().Text(text).FontSize(9).FontColor(clr);
                    if (bold) txt.Bold();
                }

                DataCellColored(t.Cell(), $"{i + 1}", Colors.Orange.Darken3, true);
                DataCellColored(t.Cell(), area.ZoneName ?? "-", Colors.Orange.Darken4, true);
                DataCellColored(t.Cell(), area.TotalOrders.ToString(), Colors.Green.Darken4, true);
                DataCell(t.Cell(), $"{area.AverageOrderValue:0.##} {Currency}");
                DataCellColored(t.Cell(), $"{area.TotalAmount:0.##} {Currency}", Colors.Orange.Darken4, true);
                DataCellColored(t.Cell(), $"{pct:0.1}%", Colors.Orange.Darken3, false);
            }

            int grandOrders = ranked.Sum(a => a.TotalOrders);
            decimal grandAvg = grandOrders > 0 ? grandTotal / grandOrders : 0;

            t.Cell().ColumnSpan(2).Background(Colors.Orange.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text(grandOrders.ToString()).FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text($"{grandAvg:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text($"{grandTotal:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text("100%").FontSize(10).Bold().FontColor(Colors.White);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Delivery Areas" : "مناطق التوصيل").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        decimal totalRevenue = _areas.Sum(a => a.TotalAmount);
        int totalOrders = _areas.Sum(a => a.TotalOrders);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Orders:" : "إجمالي الطلبات:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(totalOrders.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Revenue:" : "إجمالي الإيراد:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{totalRevenue:0.##} {Currency}").FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var area in _areas.OrderByDescending(a => a.TotalAmount))
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"{area.ZoneName} ({area.TotalOrders})").FontSize(8);
                row.ConstantItem(70).AlignRight().Text($"{area.TotalAmount:0.##}").FontSize(8).Bold();
            });
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "TOTAL" : "الإجمالي").FontSize(9).Bold();
            row.ConstantItem(70).AlignRight().Text($"{totalRevenue:0.##} {Currency}").FontSize(9).Bold();
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}

