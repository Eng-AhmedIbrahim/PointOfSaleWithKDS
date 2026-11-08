using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeHourlySalesDocument : IDocument
{
    private readonly List<HourlySalesDto> _data;
    private readonly string _branchName;
    private readonly DateTime _reportDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeHourlySalesDocument(List<HourlySalesDto> data, string branchName, DateTime reportDate, bool isThermal = false, string lang = "ar", string currency = "EGP")
    {
        _data        = data ?? new List<HourlySalesDto>();
        _branchName  = branchName;
        _reportDate  = reportDate;
        _isThermal   = isThermal;
        _isEnglish   = (lang ?? "ar").ToLower() == "en";
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

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Teal.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Hourly Sales Analysis" : "تحليل المبيعات بالساعة").FontSize(10).FontColor(Colors.Teal.Lighten4);
                });
                row.ConstantItem(180).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"Date: {_reportDate:dd/MM/yyyy}"
                        : $"التاريخ: {_reportDate:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Teal.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).Row(row =>
            {
                row.RelativeItem().Text(_isEnglish ? "Hourly Sales Analysis" : "تحليل المبيعات بالساعة")
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
        decimal totalAmount  = _data.Sum(x => x.Amount);
        int     totalOrders  = _data.Sum(x => x.OrderCount);
        var     peakAmtHour  = _data.OrderByDescending(x => x.Amount).FirstOrDefault();
        var     peakOrdHour  = _data.OrderByDescending(x => x.OrderCount).FirstOrDefault();
        decimal avgPerOrder  = totalOrders > 0 ? totalAmount / totalOrders : 0;

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي المبيعات",
                $"{totalAmount:0.##} {Currency}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Orders" : "إجمالي الطلبات",
                $"{totalOrders}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Peak Revenue Hour" : "أعلى ساعة مبيعات",
                $"{peakAmtHour?.HourLabel ?? "-"}", Colors.Orange.Darken3, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Busiest Hour" : "أكثر ساعة ازدحاماً",
                $"{peakOrdHour?.HourLabel ?? "-"} ({peakOrdHour?.OrderCount ?? 0})", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg / Order" : "متوسط الطلب",
                $"{avgPerOrder:0.##} {Currency}", Colors.Teal.Darken3, Colors.Teal.Lighten5);
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
        if (!_data.Any()) return;
        decimal maxAmount = _data.Max(x => x.Amount);

        col.Item().PaddingBottom(10).Column(inner =>
        {
            inner.Item().Text(_isEnglish ? "Hourly Revenue Distribution" : "توزيع الإيراد بالساعة")
                .FontSize(11).Bold().FontColor(Colors.Teal.Darken3);
            inner.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(70);   // Hour label
                    cd.RelativeColumn();     // Bar
                    cd.ConstantColumn(90);   // Amount
                    cd.ConstantColumn(45);   // Orders
                });

                foreach (var h in _data)
                {
                    float pct = maxAmount > 0 ? (float)(h.Amount / maxAmount) : 0;
                    bool isPeak = h.Amount == maxAmount && maxAmount > 0;
                    var barColor = isPeak ? Colors.Orange.Darken2 : Colors.Teal.Medium;

                    t.Cell().Padding(2).AlignRight().Text(h.HourLabel).FontSize(8)
                        .FontColor(isPeak ? Colors.Orange.Darken3 : Colors.Black);
                    t.Cell().Padding(2).Column(c =>
                    {
                        c.Item().Height(14).Row(r =>
                        {
                            r.RelativeItem(Math.Max(0.01f, pct)).Background(barColor).Height(10);
                            r.RelativeItem(Math.Max(0.01f, 1 - pct)).Height(10);
                        });
                    });
                    t.Cell().Padding(2).AlignRight()
                        .Text($"{h.Amount:0.##} {Currency}").FontSize(8).Bold()
                        .FontColor(isPeak ? Colors.Orange.Darken3 : Colors.Black);
                    t.Cell().Padding(2).AlignCenter()
                        .Text($"{h.OrderCount} {(_isEnglish ? "ord" : "طلب")}").FontSize(8);
                }
            });
        });
    }

    private void ComposeA4Table(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(2);    // Hour
                cd.RelativeColumn(2);    // Dine-In
                cd.RelativeColumn(2);    // TakeAway
                cd.RelativeColumn(2);    // Delivery
                cd.RelativeColumn(2);    // Total
                cd.RelativeColumn(1.5f); // Orders
                cd.RelativeColumn(2);    // Avg
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Teal.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);
                Hdr(h.Cell(), _isEnglish ? "Hour" : "الساعة");
                Hdr(h.Cell(), _isEnglish ? "Dine-In" : "داخلي");
                Hdr(h.Cell(), _isEnglish ? "TakeAway" : "خارجي");
                Hdr(h.Cell(), _isEnglish ? "Delivery" : "توصيل");
                Hdr(h.Cell(), _isEnglish ? "Total" : "الإجمالي");
                Hdr(h.Cell(), _isEnglish ? "Orders" : "الطلبات");
                Hdr(h.Cell(), _isEnglish ? "Avg" : "متوسط");
            });

            decimal totalAmount = _data.Sum(x => x.Amount);
            decimal maxAmount   = _data.Any() ? _data.Max(x => x.Amount) : 0;

            for (int i = 0; i < _data.Count; i++)
            {
                var h = _data[i];
                bool isPeak = h.Amount == maxAmount && maxAmount > 0;
                var bgColor = isPeak ? Colors.Orange.Lighten5 : (i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5);
                decimal avg = h.OrderCount > 0 ? h.Amount / h.OrderCount : 0;

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

                if (isPeak)
                    DataCellColored(t.Cell(), h.HourLabel, Colors.Orange.Darken3, true);
                else
                    DataCellColored(t.Cell(), h.HourLabel, Colors.Teal.Darken2, false);

                DataCell(t.Cell(), h.DineInAmount > 0 ? $"{h.DineInAmount:0.##}" : "-");
                DataCell(t.Cell(), h.TakeAwayAmount > 0 ? $"{h.TakeAwayAmount:0.##}" : "-");
                DataCell(t.Cell(), h.DeliveryAmount > 0 ? $"{h.DeliveryAmount:0.##}" : "-");
                DataCellColored(t.Cell(), $"{h.Amount:0.##} {Currency}", isPeak ? Colors.Orange.Darken3 : Colors.Green.Darken3, true);
                DataCellColored(t.Cell(), h.OrderCount.ToString(), Colors.Teal.Darken2, false);
                DataCell(t.Cell(), $"{avg:0.##}");
            }

            decimal grandTotal = _data.Sum(x => x.Amount);
            int grandOrders = _data.Sum(x => x.OrderCount);
            decimal grandAvg = grandOrders > 0 ? grandTotal / grandOrders : 0;

            t.Cell().ColumnSpan(4).Background(Colors.Teal.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Teal.Darken4).Padding(6).AlignCenter()
                .Text($"{grandTotal:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Teal.Darken4).Padding(6).AlignCenter()
                .Text(grandOrders.ToString()).FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Teal.Darken4).Padding(6).AlignCenter()
                .Text($"{grandAvg:0.##}").FontSize(10).Bold().FontColor(Colors.White);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Hourly Sales" : "مبيعات بالساعة").FontSize(10).Bold();
        col.Item().AlignCenter().Text(_reportDate.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        decimal totalAmount = _data.Sum(x => x.Amount);
        int totalOrders = _data.Sum(x => x.OrderCount);
        var peakHour = _data.OrderByDescending(x => x.Amount).FirstOrDefault();

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Peak Hour:" : "أعلى ساعة:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text(peakHour?.HourLabel ?? "-").FontSize(8).Bold().FontColor(Colors.Orange.Darken2);
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total:" : "الإجمالي:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{totalAmount:0.##} {Currency}").FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var h in _data.Where(x => x.Amount > 0))
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(55).Text(h.HourLabel).FontSize(8);
                row.RelativeItem().Text($"{h.OrderCount} {(_isEnglish ? "ord" : "طلب")}").FontSize(8);
                row.ConstantItem(65).AlignRight().Text($"{h.Amount:0.##}").FontSize(8).Bold();
            });
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "TOTAL" : "الإجمالي").FontSize(9).Bold();
            row.ConstantItem(70).AlignRight().Text($"{totalAmount:0.##} {Currency}").FontSize(9).Bold();
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
