using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeDiscountReportDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private readonly string _reportTitle;
    private const string Currency = "ج.م";

    public BackOfficeDiscountReportDocument(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string reportTitle = "تقرير الخصومات", string lang = "ar", bool isThermal = false)
    {
        _orders      = orders ?? new List<OrderDto>();
        _branchName  = branchName;
        _fromDate    = fromDate;
        _toDate      = toDate;
        _reportTitle = reportTitle;
        _isThermal   = isThermal;
        _isEnglish   = (lang ?? "ar").ToLower() == "en";
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static decimal GetOrderDiscount(OrderDto o)
        => (o.TotalOrderDiscount ?? 0) + (o.TotalDiscount ?? 0) + (o.DiscountedItems ?? 0);

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
                    ComposeA4OrdersTable(col);
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
                    c.Item().Text(_isEnglish ? "Discounted Orders Report" : "تقرير الخصومات والتخفيضات").FontSize(10).FontColor(Colors.Orange.Lighten4);
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
            row.RelativeItem().Text(_isEnglish ? "Discounted Orders Report" : "تقرير الخصومات والتخفيضات")
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
        decimal totalDiscount = _orders.Sum(o => GetOrderDiscount(o));
        int orderCount = _orders.Count;
        decimal totalSales = _orders.Sum(o => o.GrandTotal ?? 0);
        decimal avgDiscount = orderCount > 0 ? totalDiscount / orderCount : 0;
        decimal maxDiscount = _orders.Any() ? _orders.Max(o => GetOrderDiscount(o)) : 0;

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Discount" : "إجمالي الخصومات",
                $"{totalDiscount:0.##} {Currency}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Discounted Orders" : "عدد الطلبات المخصومة",
                $"{orderCount}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Sales Value" : "إجمالي قيمة الطلبات",
                $"{totalSales:0.##} {Currency}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg Discount" : "متوسط الخصم",
                $"{avgDiscount:0.##} {Currency}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Max Discount" : "أعلى خصم",
                $"{maxDiscount:0.##} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5);
        });
    }

    private static void KpiCard(IContainer c, string label, string value, Color textColor, Color bgColor)
    {
        c.Background(bgColor).Border(1).BorderColor(textColor).Padding(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(textColor);
            col.Item().Text(value).FontSize(11).Bold().FontColor(textColor);
        });
    }

    private void ComposeA4OrdersTable(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(50);   // Order#
                cd.RelativeColumn(1.8f); // Type
                cd.RelativeColumn(2);    // Cashier / By
                cd.RelativeColumn(2);    // Date & Time
                cd.RelativeColumn(2);    // Total
                cd.RelativeColumn(2);    // Discount
                cd.RelativeColumn(1.5f); // Disc %
                cd.RelativeColumn(3);    // Reason
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Orange.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), _isEnglish ? "Order#" : "رقم");
                Hdr(h.Cell(), _isEnglish ? "Type" : "النوع");
                Hdr(h.Cell(), _isEnglish ? "Cashier" : "الكاشير");
                Hdr(h.Cell(), _isEnglish ? "Date & Time" : "التاريخ");
                Hdr(h.Cell(), _isEnglish ? "Total" : "الإجمالي");
                Hdr(h.Cell(), _isEnglish ? "Discount" : "الخصم");
                Hdr(h.Cell(), _isEnglish ? "%" : "النسبة");
                Hdr(h.Cell(), _isEnglish ? "Reason" : "السبب");
            });

            int rowIdx = 0;
            foreach (var order in _orders.OrderBy(o => o.OrderDate))
            {
                decimal disc = GetOrderDiscount(order);
                decimal total = order.GrandTotal ?? 0;
                decimal original = total + disc;
                decimal pct = original > 0 ? (disc / original * 100) : 0;
                var bgColor = rowIdx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

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

                DataCellColored(t.Cell(), $"#{order.OrderId}", Colors.Orange.Darken3, true);
                DataCell(t.Cell(), order.OrderType ?? "-");
                DataCell(t.Cell(), order.DiscountByName ?? order.CashierName ?? "-");
                DataCell(t.Cell(), order.OrderDate?.ToString("dd/MM/yyyy HH:mm") ?? "-");
                DataCellColored(t.Cell(), $"{total:0.##} {Currency}", Colors.Green.Darken3, true);
                DataCellColored(t.Cell(), $"{disc:0.##} {Currency}", Colors.Orange.Darken4, true);
                DataCellColored(t.Cell(), $"{pct:0.1}%", Colors.Orange.Darken3, false);
                DataCell(t.Cell(), order.DiscountReason ?? "-");

                rowIdx++;
            }

            decimal totalAllSales = _orders.Sum(o => o.GrandTotal ?? 0);
            decimal totalAllDisc = _orders.Sum(o => GetOrderDiscount(o));
            decimal overallOriginal = totalAllSales + totalAllDisc;
            decimal overallPct = overallOriginal > 0 ? (totalAllDisc / overallOriginal * 100) : 0;

            t.Cell().ColumnSpan(4).Background(Colors.Orange.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text($"{totalAllSales:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text($"{totalAllDisc:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6).AlignCenter()
                .Text($"{overallPct:0.1}%").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Orange.Darken4).Padding(6);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Discounts Report" : "تقرير الخصومات").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        decimal totalDiscount = _orders.Sum(o => GetOrderDiscount(o));
        int orderCount = _orders.Count;

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Orders Count:" : "عدد الطلبات:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(orderCount.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Discounts:" : "إجمالي الخصومات:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{totalDiscount:0.##} {Currency}").FontSize(8).Bold().FontColor(Colors.Orange.Darken3);
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var order in _orders.OrderBy(o => o.OrderDate))
        {
            decimal disc = GetOrderDiscount(order);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"#{order.OrderId} {order.OrderType}").FontSize(8);
                row.ConstantItem(70).AlignRight().Text($"-{disc:0.##} {Currency}").FontSize(8).Bold().FontColor(Colors.Orange.Darken3);
            });
            if (!string.IsNullOrEmpty(order.DiscountReason))
            {
                col.Item().PaddingLeft(8).Text($"→ {order.DiscountReason}").FontSize(7).FontColor(Colors.Grey.Darken2);
            }
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "TOTAL DISCOUNT" : "إجمالي الخصم").FontSize(9).Bold();
            row.ConstantItem(70).AlignRight().Text($"{totalDiscount:0.##} {Currency}").FontSize(9).Bold().FontColor(Colors.Orange.Darken3);
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
