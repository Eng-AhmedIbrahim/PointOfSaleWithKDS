using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeSalesDetailedDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _lang;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeSalesDetailedDocument(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar", bool isThermal = false)
    {
        _orders     = orders ?? new List<OrderDto>();
        _branchName = branchName;
        _fromDate   = fromDate;
        _toDate     = toDate;
        _lang       = (lang ?? "ar").ToLower();
        _isEnglish  = _lang == "en";
        _isThermal  = isThermal;
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
                col.Spacing(_isThermal ? 3 : 6);
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
            col.Item().Background(Colors.Blue.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Detailed Sales Report" : "تقرير المبيعات التفصيلي للأوردرات والأصناف").FontSize(10).FontColor(Colors.Blue.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"الفترة من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Blue.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).Row(row =>
            {
                row.RelativeItem().Text(_isEnglish ? "Detailed Sales Report" : "تقرير المبيعات التفصيلي")
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
        var completed = _orders.Where(o => o.OrderState == "Completed").ToList();
        decimal totalRevenue = completed.Sum(o => o.GrandTotal ?? 0);
        decimal totalDiscount = completed.Sum(o => o.TotalDiscount ?? o.DiscountedItems ?? 0);
        int orderCount = completed.Count;
        decimal avgOrder = orderCount > 0 ? totalRevenue / orderCount : 0;
        int voidCount = _orders.Count(o => o.OrderState == "Voided");
        decimal voidAmount = _orders.Sum(o => o.VoidAmount ?? (o.OrderState == "Voided" ? o.GrandTotal ?? 0 : 0));

        col.Item().PaddingBottom(6).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي المبيعات",
                $"{totalRevenue:N2} {Currency}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Orders Count" : "عدد الطلبات",
                $"{orderCount:N0}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg. Order" : "متوسط الطلب",
                $"{avgOrder:N2} {Currency}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Voids" : "إجمالي الإلغاء",
                $"{voidAmount:N2} {Currency} ({voidCount})", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Discounts" : "إجمالي الخصم",
                $"{totalDiscount:N2} {Currency}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
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

    private void ComposeA4OrdersTable(ColumnDescriptor col)
    {
        col.Item().Column(ordersCol =>
        {
            ordersCol.Spacing(8); // Clear visual space between orders

            foreach (var order in _orders.OrderBy(o => o.OrderDate))
            {
                bool isVoided = order.OrderState == "Voided";
                decimal discount = order.TotalDiscount ?? order.DiscountedItems ?? 0;
                var headerBg = isVoided ? Colors.Red.Darken2 : Colors.Blue.Darken3;
                var cardBorder = isVoided ? Colors.Red.Medium : Colors.Blue.Lighten2;

                ordersCol.Item().Border(1).BorderColor(cardBorder).Background(Colors.White).Column(card =>
                {
                    // 1. Order Header Bar
                    card.Item().Background(headerBg).Padding(5).Row(hr =>
                    {
                        hr.RelativeItem().Text(txt =>
                        {
                            txt.Span($"#{order.OrderId} ").Bold().FontSize(10).FontColor(Colors.White);
                            txt.Span($"({order.OrderType ?? "-"}) ").FontSize(8.5f).FontColor(Colors.Blue.Lighten4);
                            if (isVoided)
                                txt.Span(_isEnglish ? " [VOIDED]" : " [ملغي]").Bold().FontSize(8.5f).FontColor(Colors.Yellow.Lighten2);
                        });

                        hr.ConstantItem(130).AlignCenter().Text(order.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-")
                            .FontSize(8).FontColor(Colors.White);

                        hr.ConstantItem(120).AlignCenter().Text($"الكاشير: {order.CashierName ?? order.StaffName ?? "-"}")
                            .FontSize(8).FontColor(Colors.White);

                        hr.ConstantItem(110).AlignRight().Text(txt =>
                        {
                            txt.Span("الإجمالي: ").FontSize(8).FontColor(Colors.White);
                            txt.Span($"{order.GrandTotal:N2} {Currency}").Bold().FontSize(9.5f).FontColor(Colors.Yellow.Lighten2);
                        });
                    });

                    // 2. Extra Bar if discount or notice or void amount
                    if (discount > 0 || (order.VoidAmount ?? 0) > 0 || !string.IsNullOrWhiteSpace(order.OrderNotice) || !string.IsNullOrWhiteSpace(order.DriverName))
                    {
                        card.Item().Background(Colors.Grey.Lighten4).PaddingHorizontal(6).PaddingVertical(3).Row(nr =>
                        {
                            if (discount > 0)
                                nr.RelativeItem().Text($"الخصم: {discount:N2} {Currency}").FontSize(7.5f).FontColor(Colors.Orange.Darken3).Bold();
                            if ((order.VoidAmount ?? 0) > 0)
                                nr.RelativeItem().Text($"المبلغ الملغي: {order.VoidAmount:N2} {Currency}").FontSize(7.5f).FontColor(Colors.Red.Darken3).Bold();
                            if (!string.IsNullOrWhiteSpace(order.DriverName))
                                nr.RelativeItem().Text($"الطيار: {order.DriverName}").FontSize(7.5f).FontColor(Colors.Teal.Darken3);
                            if (!string.IsNullOrWhiteSpace(order.OrderNotice))
                                nr.RelativeItem().Text($"ملاحظة: {order.OrderNotice}").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                        });
                    }

                    // 3. Items Table
                    if (order.OrderDetails != null && order.OrderDetails.Any())
                    {
                        card.Item().Padding(4).Table(it =>
                        {
                            it.ColumnsDefinition(icd =>
                            {
                                icd.RelativeColumn(4); // Item name & comments
                                icd.ConstantColumn(55);// Qty
                                icd.ConstantColumn(70);// Unit Price
                                icd.ConstantColumn(80);// Total
                            });

                            it.Header(h =>
                            {
                                void H(IContainer cell, string t) =>
                                    cell.Background(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                                        .Text(t).FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken4);

                                H(h.Cell(), _isEnglish ? "Item & Options" : "الصنف والخيارات");
                                H(h.Cell(), _isEnglish ? "Qty" : "الكمية");
                                H(h.Cell(), _isEnglish ? "Unit Price" : "سعر الوحدة");
                                H(h.Cell(), _isEnglish ? "Total" : "الإجمالي");
                            });

                            int iIdx = 0;
                            foreach (var item in order.OrderDetails)
                            {
                                var iBg = iIdx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                                string itemName = _isEnglish ? (item.Name ?? item.NameAr ?? "-") : (item.NameAr ?? item.Name ?? "-");

                                it.Cell().Background(iBg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Column(ic =>
                                {
                                    ic.Item().Text(itemName).FontSize(8).Bold();
                                    if (item.Attributes != null && item.Attributes.Any())
                                    {
                                        foreach (var attr in item.Attributes)
                                        {
                                            string attrPrice = attr.ExtraPrice > 0 ? $" (+{attr.ExtraPrice:N2})" : "";
                                            ic.Item().Text($"+ {attr.Name}{attrPrice}").FontSize(7).FontColor(Colors.Grey.Darken2).Italic();
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(item.LineComment))
                                        ic.Item().Text($"* {item.LineComment}").FontSize(7).FontColor(Colors.Grey.Darken3);
                                });

                                it.Cell().Background(iBg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                                    .Text(item.Quantity.ToString("G29")).FontSize(8);

                                it.Cell().Background(iBg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                                    .Text($"{item.Price:N2}").FontSize(8);

                                it.Cell().Background(iBg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignCenter()
                                    .Text($"{item.Total:N2}").FontSize(8).Bold();

                                iIdx++;
                            }
                        });
                    }
                });
            }

            // Grand Total Bottom Bar
            decimal grandRevenue = _orders.Where(o => o.OrderState == "Completed").Sum(o => o.GrandTotal ?? 0);
            ordersCol.Item().Background(Colors.Blue.Darken4).Padding(6).Row(gr =>
            {
                gr.RelativeItem().Text(_isEnglish ? "Grand Total (Completed Orders):" : "إجمالي المبيعات للطلبات المكتملة:")
                    .FontSize(9.5f).Bold().FontColor(Colors.White);
                gr.ConstantItem(150).AlignRight().Text($"{grandRevenue:N2} {Currency}")
                    .FontSize(11).Bold().FontColor(Colors.Yellow.Lighten2);
            });
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Detailed Sales" : "المبيعات التفصيلية").FontSize(9).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        var completed = _orders.Where(o => o.OrderState == "Completed").ToList();
        decimal totalRevenue = completed.Sum(o => o.GrandTotal ?? 0);
        int orderCount = completed.Count;

        col.Item().PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Orders Count:" : "عدد الطلبات:").FontSize(7.5f).Bold();
            row.RelativeItem().AlignRight().Text(orderCount.ToString()).FontSize(7.5f).Bold();
        });
        col.Item().PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Sales:" : "إجمالي المبيعات:").FontSize(7.5f).Bold();
            row.RelativeItem().AlignRight().Text($"{totalRevenue:N2} {Currency}").FontSize(7.5f).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Black);

        foreach (var order in _orders.OrderBy(o => o.OrderDate))
        {
            bool isVoided = order.OrderState == "Voided";

            col.Item().PaddingTop(2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(2).Column(oCol =>
            {
                // Order Header line
                oCol.Item().Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span($"#{order.OrderId} ").Bold().FontSize(8);
                        t.Span($"({order.OrderType}) ").FontSize(7.5f);
                        if (isVoided)
                        {
                            t.Span(_isEnglish ? "[ملغي]" : "[ملغي]").Bold().FontSize(7.5f).FontColor(Colors.Red.Darken2);
                        }
                    });
                    r.ConstantItem(60).AlignRight().Text($"{order.GrandTotal:0.##} {Currency}")
                        .Bold().FontSize(8).FontColor(isVoided ? Colors.Red.Darken2 : Colors.Green.Darken3);
                });

                // Cashier and Time
                oCol.Item().Row(r =>
                {
                    string timeStr = order.OrderDate?.ToString("hh:mm tt") ?? "";
                    string cashierStr = !string.IsNullOrEmpty(order.CashierName) ? order.CashierName : "-";
                    r.RelativeItem().Text($"{timeStr} | {cashierStr}").FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                // Items list in a clean, bordered table!
                if (order.OrderDetails != null && order.OrderDetails.Any())
                {
                    oCol.Item().PaddingTop(2).Table(it =>
                    {
                        it.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3); // Item
                            cd.ConstantColumn(24);// Qty
                            cd.ConstantColumn(36);// Total
                        });

                        it.Header(h =>
                        {
                            h.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).Text(_isEnglish ? "Item" : "الصنف").FontSize(6.5f).Bold();
                            h.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).AlignCenter().Text(_isEnglish ? "Qty" : "الكمية").FontSize(6.5f).Bold();
                            h.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).AlignRight().Text(_isEnglish ? "Total" : "الإجمالي").FontSize(6.5f).Bold();
                        });

                        foreach (var item in order.OrderDetails)
                        {
                            string itemName = _isEnglish ? (item.Name ?? item.NameAr) : (item.NameAr ?? item.Name);

                            it.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(1).Column(ic =>
                            {
                                ic.Item().Text(itemName ?? "-").FontSize(7).Bold();
                                if (item.Attributes != null && item.Attributes.Any())
                                {
                                    foreach (var attr in item.Attributes)
                                    {
                                        string attrPrice = attr.ExtraPrice > 0 ? $" (+{attr.ExtraPrice:0.##})" : "";
                                        ic.Item().Text($"+ {attr.Name}{attrPrice}").FontSize(6).FontColor(Colors.Grey.Darken2);
                                    }
                                }
                                if (!string.IsNullOrWhiteSpace(item.LineComment))
                                {
                                    ic.Item().Text($"* {item.LineComment}").FontSize(6).FontColor(Colors.Grey.Darken2);
                                }
                            });

                            it.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(1).AlignCenter()
                                .Text(item.Quantity.ToString("G29")).FontSize(7).Bold();

                            it.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(1).AlignRight()
                                .Text($"{item.Total:0.##}").FontSize(7);
                        }
                    });
                }
            });
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        col.Item().AlignCenter().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
    }
}
