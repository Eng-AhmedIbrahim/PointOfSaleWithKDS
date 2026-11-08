using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeDetailedDiscountDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _reportTitle;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeDetailedDiscountDocument(
        List<OrderDto> orders,
        string branchName,
        DateTime fromDate,
        DateTime toDate,
        string reportTitle = "تقرير الخصومات التفصيلي",
        string lang = "ar",
        bool isThermal = false)
    {
        _orders = orders ?? new List<OrderDto>();
        _branchName = branchName;
        _fromDate = fromDate;
        _toDate = toDate;
        _reportTitle = reportTitle;
        _isThermal = isThermal;
        _isEnglish = (lang ?? "ar").ToLower() == "en";
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

            var baseStyle = TextStyle.Default.FontFamily("Arial", "Noto Sans Arabic").FontSize(_isThermal ? 8 : 10);
            page.DefaultTextStyle(baseStyle);

            if (!_isEnglish)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 3 : 6);
                ComposeHeader(col);
                ComposeDiscountSummary(col);
                ComposeOrdersList(col);
                ComposeFooter(col);
            });
        });
    }

    private void ComposeHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(_isThermal ? 11 : 14).Bold().FontColor(Colors.Black);
        col.Item().AlignCenter().Text(_reportTitle).FontSize(_isThermal ? 12 : 18).Bold().FontColor(Colors.Black);
        col.Item().AlignCenter().Text(_isEnglish ? $"Period: {_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}" : $"الفترة من: {_fromDate:dd/MM/yyyy} إلى: {_toDate:dd/MM/yyyy}").FontSize(_isThermal ? 7 : 10).FontColor(Colors.Grey.Darken2);
        col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Black);
    }

    private decimal GetOrderDiscount(OrderDto o)
    {
        decimal disc = (o.TotalOrderDiscount ?? 0) + (o.TotalDiscount ?? 0) + (o.DiscountedItems ?? 0);
        if (disc <= 0 && o.OrderDetails != null && o.OrderDetails.Any())
        {
            disc = o.OrderDetails.Sum(i =>
                (i.DiscountAmount ?? 0) > 0 ? (i.DiscountAmount.Value * i.Quantity) :
                ((i.DiscountPercentage ?? 0) > 0 ? (i.Price ?? 0) * (i.DiscountPercentage.Value / 100m) * i.Quantity :
                ((i.TotalDiscountPrice ?? 0) > 0 ? i.TotalDiscountPrice.Value : 0)));
        }
        return disc;
    }

    private void ComposeDiscountSummary(ColumnDescriptor col)
    {
        var totalDiscount = _orders.Sum(o => GetOrderDiscount(o));
        var orderCount = _orders.Count;
        var totalGross = _orders.Sum(o => (o.GrandTotal ?? 0) + GetOrderDiscount(o));
        var totalNet = _orders.Sum(o => o.GrandTotal ?? 0);

        if (_isThermal)
        {
            col.Item().Background(Colors.Grey.Lighten4).Padding(4).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(_isEnglish ? "Orders Count:" : "عدد الطلبات:").Bold().FontSize(8);
                    r.RelativeItem().AlignRight().Text(orderCount.ToString()).Bold().FontSize(8);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(_isEnglish ? "Total Discounts:" : "إجمالي الخصومات:").Bold().FontSize(8);
                    r.RelativeItem().AlignRight().Text($"{totalDiscount:0.##}").Bold().FontSize(9).FontColor(Colors.Red.Darken2);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(_isEnglish ? "Total Net:" : "صافي المبيعات:").Bold().FontSize(8);
                    r.RelativeItem().AlignRight().Text($"{totalNet:0.##}").Bold().FontSize(8);
                });
            });
        }
        else
        {
            col.Item().PaddingVertical(3).Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text(_isEnglish ? "Orders with Discount" : "عدد طلبات الخصم").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignCenter().Text(orderCount.ToString()).FontSize(13).Bold();
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text(_isEnglish ? "Total Discounts" : "إجمالي قيمة الخصم").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignCenter().Text($"{totalDiscount:0.##}").FontSize(14).Bold().FontColor(Colors.Red.Darken2);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text(_isEnglish ? "Gross Sales" : "الإجمالي قبل الخصم").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignCenter().Text($"{totalGross:0.##}").FontSize(13).Bold();
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text(_isEnglish ? "Net Sales" : "الصافي بعد الخصم").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignCenter().Text($"{totalNet:0.##}").FontSize(13).Bold().FontColor(Colors.Green.Darken3);
                });
            });
        }
    }

    private void ComposeOrdersList(ColumnDescriptor col)
    {
        foreach (var order in _orders)
        {
            var disc = GetOrderDiscount(order);
            var netTotal = order.GrandTotal ?? 0;
            var grossTotal = netTotal + disc;

            if (_isThermal)
            {
                ComposeThermalOrderCard(col, order, disc, grossTotal, netTotal);
            }
            else
            {
                ComposeA4OrderCard(col, order, disc, grossTotal, netTotal);
            }
        }
    }

    private void ComposeA4OrderCard(ColumnDescriptor col, OrderDto order, decimal disc, decimal grossTotal, decimal netTotal)
    {
        col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White).Column(card =>
        {
            // 1. Order Header Bar
            card.Item().Background(Colors.Grey.Lighten3).Padding(4).Row(row =>
            {
                row.ConstantItem(80).Text($"#{order.OrderId}").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                row.RelativeItem().Text($"{order.OrderType}").Bold().FontSize(9);
                row.RelativeItem().Text(order.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-").FontSize(8).FontColor(Colors.Grey.Darken2);
                row.RelativeItem().Text($"{(_isEnglish ? "Cashier: " : "الكاشير: ")}{order.CashierName ?? order.StaffName ?? "-"}").FontSize(9);
                row.ConstantItem(120).AlignRight().Text($"{(_isEnglish ? "Net: " : "الصافي: ")}{netTotal:0.##}").Bold().FontSize(10).FontColor(Colors.Green.Darken3);
            });

            // 2. Discount Info Strip
            card.Item().Background(Colors.Orange.Lighten5).Padding(4).Row(row =>
            {
                string discDesc = $"{(_isEnglish ? "Discount: " : "الخصم: ")}{disc:0.##}";
                if ((order.DiscountPercentage ?? 0) > 0)
                    discDesc += $" ({order.DiscountPercentage:0.##}%)";

                row.RelativeItem(2).Text(discDesc).Bold().FontSize(9).FontColor(Colors.Red.Darken2);

                string authBy = order.DiscountByName ?? order.DiscountBy ?? "-";
                row.RelativeItem(2).Text($"{(_isEnglish ? "By: " : "المسؤول: ")}{authBy}").FontSize(8).Bold();

                string reason = !string.IsNullOrWhiteSpace(order.DiscountReason) ? order.DiscountReason : (!string.IsNullOrWhiteSpace(order.DiscountType) ? order.DiscountType : "-");
                row.RelativeItem(3).Text($"{(_isEnglish ? "Reason: " : "السبب: ")}{reason}").FontSize(8);

                row.RelativeItem(2).AlignRight().Text($"{(_isEnglish ? "Before: " : "قبل: ")}{grossTotal:0.##}").FontSize(8).FontColor(Colors.Grey.Darken2);
            });

            // 3. Items Table
            var items = order.OrderDetails ?? new List<POS.Contract.Models.TableItem>();
            if (items.Any())
            {
                card.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(4);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(3);
                    });

                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignRight().Text(_isEnglish ? "Item" : "الصنف").Bold().FontSize(8);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isEnglish ? "Qty" : "الكمية").Bold().FontSize(8);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isEnglish ? "Price" : "السعر").Bold().FontSize(8);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isEnglish ? "Item Disc." : "خصم الصنف").Bold().FontSize(8);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isEnglish ? "Total" : "الإجمالي").Bold().FontSize(8);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(2).AlignRight().Text(_isEnglish ? "Reason / Note" : "السبب / ملاحظات").Bold().FontSize(8);
                    });

                    foreach (var itm in items)
                    {
                        decimal itemDisc = (itm.DiscountAmount ?? 0) > 0 ? (itm.DiscountAmount.Value * itm.Quantity) :
                                           ((itm.DiscountPercentage ?? 0) > 0 ? (itm.Price ?? 0) * (itm.DiscountPercentage.Value / 100m) * itm.Quantity :
                                           ((itm.TotalDiscountPrice ?? 0) > 0 ? itm.TotalDiscountPrice.Value : 0));

                        string itemDiscStr = itemDisc > 0 ? $"{itemDisc:0.##}" : "-";
                        if ((itm.DiscountPercentage ?? 0) > 0)
                            itemDiscStr += $" ({itm.DiscountPercentage:0.##}%)";

                        decimal lineTotal = itm.TotalAfterDiscount ?? itm.Total ?? ((itm.Quantity * (itm.Price ?? 0)) - itemDisc);

                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(_isEnglish ? (itm.Name ?? itm.NameAr) : (itm.NameAr ?? itm.Name)).FontSize(8);
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(itm.Quantity.ToString("G29")).FontSize(8);
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(itm.Price?.ToString("0.##") ?? "0").FontSize(8);

                        if (itemDisc > 0)
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(itemDiscStr).Bold().FontSize(8).FontColor(Colors.Red.Darken2);
                        else
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text("-").FontSize(8).FontColor(Colors.Grey.Darken1);

                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(lineTotal.ToString("0.##")).Bold().FontSize(8);

                        string note = itm.DiscountReason ?? itm.LineComment ?? "-";
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(note).FontSize(7).FontColor(Colors.Grey.Darken2);
                    }
                });
            }
        });
    }

    private void ComposeThermalOrderCard(ColumnDescriptor col, OrderDto order, decimal disc, decimal grossTotal, decimal netTotal)
    {
        col.Item().PaddingTop(3).Border(1).BorderColor(Colors.Black).Padding(2).Column(card =>
        {
            card.Item().Row(r =>
            {
                r.RelativeItem().Text($"#{order.OrderId} ({order.OrderType})").Bold().FontSize(8);
                r.RelativeItem().AlignRight().Text(order.OrderDate?.ToString("HH:mm") ?? "").FontSize(7);
            });

            card.Item().Text($"{(_isEnglish ? "Cashier: " : "الكاشير: ")}{order.CashierName ?? order.StaffName ?? "-"}").FontSize(7);

            string auth = order.DiscountByName ?? order.DiscountBy ?? "-";
            string reason = order.DiscountReason ?? order.DiscountType ?? "-";
            card.Item().Text($"{(_isEnglish ? "Disc: " : "الخصم: ")}{disc:0.##} ({reason} - {auth})").Bold().FontSize(7).FontColor(Colors.Black);

            var items = order.OrderDetails ?? new List<POS.Contract.Models.TableItem>();
            if (items.Any())
            {
                card.Item().PaddingTop(2).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(5);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(3);
                    });

                    foreach (var itm in items)
                    {
                        decimal lineTotal = itm.TotalAfterDiscount ?? itm.Total ?? (itm.Quantity * (itm.Price ?? 0));
                        t.Cell().AlignRight().Text(_isEnglish ? (itm.Name ?? itm.NameAr) : (itm.NameAr ?? itm.Name)).FontSize(7);
                        t.Cell().AlignCenter().Text(itm.Quantity.ToString("G29")).FontSize(7);
                        t.Cell().AlignRight().Text(lineTotal.ToString("0.##")).FontSize(7);
                    }
                });
            }

            card.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Text($"{(_isEnglish ? "Before: " : "قبل: ")}{grossTotal:0.##}").FontSize(7);
                r.RelativeItem().AlignRight().Text($"{(_isEnglish ? "Net: " : "الصافي: ")}{netTotal:0.##}").Bold().FontSize(8);
            });
        });
    }

    private void ComposeFooter(ColumnDescriptor col)
    {
        col.Item().PaddingTop(10).AlignCenter().Text(text =>
        {
            text.Span(_isEnglish ? "Printed at: " : "طبع في: ").FontSize(7);
            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(7);
        });
    }
}
