using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using POS.Contract.Dtos.OrderDtos;
using POS.Contract.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeVoidReportDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly List<VoidEventDto> _voidEvents;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _reportTitle;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeVoidReportDocument(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string title = "تقرير الإلغاءات", string lang = "ar", bool isThermal = false, List<VoidEventDto>? voidEvents = null)
    {
        _orders      = orders ?? new List<OrderDto>();
        _voidEvents  = voidEvents ?? new List<VoidEventDto>();
        _branchName  = branchName;
        _fromDate    = fromDate;
        _toDate      = toDate;
        _reportTitle = title;
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
                col.Spacing(_isThermal ? 4 : 8);
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
            col.Item().Background(Colors.Red.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_reportTitle).FontSize(10).FontColor(Colors.Red.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Red.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
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
        var uniqueOrders = _orders.GroupBy(o => o.OrderId).Select(g => g.First()).ToList();
        int voidCount = uniqueOrders.Count;
        decimal totalVoided = uniqueOrders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? o.GrandTotal ?? 0 : 0));
        int fullVoids = uniqueOrders.Count(o => o.OrderState == "Voided");
        int partialVoids = uniqueOrders.Count(o => o.OrderState != "Voided");

        col.Item().PaddingBottom(4).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Voided" : "إجمالي الإلغاءات",
                $"{totalVoided:0.##} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Void Orders" : "عدد الطلبات الملغاة",
                $"{voidCount}", Colors.Red.Darken3, Colors.Red.Lighten4);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Full Voids" : "إلغاء كامل",
                $"{fullVoids}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Partial Voids" : "إلغاء جزئي",
                $"{partialVoids}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
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
        if (!_orders.Any())
        {
            col.Item().Padding(20).AlignCenter().Text(_isEnglish ? "No voided orders found" : "لا توجد عمليات حذف في هذه الفترة").FontSize(11).FontColor(Colors.Grey.Medium);
            return;
        }

        var uniqueOrders = _orders.GroupBy(o => o.OrderId).Select(g => g.First()).ToList();

        foreach (var o in uniqueOrders)
        {
            bool isFullVoid = o.OrderState == "Voided";
            decimal voidedAmt = (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (isFullVoid ? (o.GrandTotal ?? 0) : 0);
            decimal beforeAmt = isFullVoid ? (voidedAmt > 0 ? voidedAmt : (o.GrandTotal ?? o.SubTotal ?? 0)) : ((o.GrandTotal ?? 0) + voidedAmt);
            decimal afterAmt = isFullVoid ? 0 : (o.GrandTotal ?? 0);

            var items = (o.OrderDetails ?? new List<TableItem>())
                .Where(i => i.IsVoided || (i.VoidAmount ?? 0) > 0 || (i.TotalVoidAmount ?? 0) > 0)
                .ToList();

            if (!items.Any() && isFullVoid)
            {
                items = (o.OrderDetails ?? new List<TableItem>()).ToList();
            }

            col.Item().Border(1).BorderColor(Colors.Red.Lighten3).Background(Colors.White).Column(card =>
            {
                // Top Header Row
                card.Item().Background(Colors.Red.Lighten5).Padding(6).Row(hr =>
                {
                    hr.RelativeItem().Row(r =>
                    {
                        r.AutoItem().Text($"#{o.OrderId}").FontSize(10).Bold().FontColor(Colors.Red.Darken4);
                        r.AutoItem().PaddingHorizontal(6).Text("|").FontColor(Colors.Grey.Lighten1);
                        r.AutoItem().Text(o.OrderType ?? "-").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                        r.AutoItem().PaddingHorizontal(6).Text("|").FontColor(Colors.Grey.Lighten1);
                        r.AutoItem().Text(isFullVoid ? (_isEnglish ? "Full Void" : "إلغاء كامل") : (_isEnglish ? "Partial Void" : "إلغاء جزئي"))
                            .FontSize(8).Bold().FontColor(isFullVoid ? Colors.Red.Darken4 : Colors.Orange.Darken4);
                        r.AutoItem().PaddingHorizontal(6).Text("|").FontColor(Colors.Grey.Lighten1);
                        r.AutoItem().Text(o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-").FontSize(8).FontColor(Colors.Grey.Darken2);
                        r.AutoItem().PaddingHorizontal(6).Text("|").FontColor(Colors.Grey.Lighten1);
                        r.AutoItem().Text($"{(_isEnglish ? "Cashier: " : "الكاشير: ")}{o.CashierName ?? o.StaffName ?? "-"}").FontSize(8).FontColor(Colors.Grey.Darken3);
                    });

                    hr.AutoItem().Row(vr =>
                    {
                        vr.AutoItem().Text(t =>
                        {
                            t.Span(_isEnglish ? "Voided By: " : "تم الحذف بواسطة: ").FontSize(8).Bold();
                            t.Span(o.VoidByName ?? "-").FontSize(8).Bold().FontColor(Colors.Red.Darken3);
                        });
                    });
                });

                // Financials & Reason Bar
                card.Item().Background(Colors.Grey.Lighten5).PaddingVertical(4).PaddingHorizontal(6).Row(fr =>
                {
                    fr.RelativeItem().Text(t =>
                    {
                        t.Span(_isEnglish ? "Order Reason: " : "سبب الإلغاء: ").Bold().FontSize(8);
                        t.Span(o.VoidReason ?? "-").FontSize(8).FontColor(Colors.Grey.Darken4);
                    });

                    fr.AutoItem().Row(mr =>
                    {
                        mr.AutoItem().Text(t =>
                        {
                            t.Span(_isEnglish ? "Before: " : "قبل: ").FontSize(8);
                            t.Span($"{beforeAmt:0.##} {Currency}").FontSize(8).Bold();
                        });
                        mr.AutoItem().PaddingHorizontal(8).Text("|").FontColor(Colors.Grey.Lighten1);
                        mr.AutoItem().Text(t =>
                        {
                            t.Span(_isEnglish ? "Voided: " : "الملغي: ").FontSize(8).FontColor(Colors.Red.Darken3);
                            t.Span($"{voidedAmt:0.##} {Currency}").FontSize(8).Bold().FontColor(Colors.Red.Darken4);
                        });
                        mr.AutoItem().PaddingHorizontal(8).Text("|").FontColor(Colors.Grey.Lighten1);
                        mr.AutoItem().Text(t =>
                        {
                            t.Span(_isEnglish ? "After: " : "بعد: ").FontSize(8);
                            t.Span($"{afterAmt:0.##} {Currency}").FontSize(8).Bold();
                        });
                    });
                });

                // Items Table
                if (items.Any())
                {
                    card.Item().Padding(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);    // Item Name
                            cd.RelativeColumn(1);    // Void Qty
                            cd.RelativeColumn(1.2f); // Price
                            cd.RelativeColumn(1.5f); // Total Void Value
                            cd.RelativeColumn(2.5f); // Reason
                            cd.RelativeColumn(2);    // Voided By
                        });

                        t.Header(h =>
                        {
                            void HCell(IContainer c, string title) => c.Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(title).FontSize(7).Bold().FontColor(Colors.Grey.Darken4);
                            HCell(h.Cell(), _isEnglish ? "Item" : "الصنف");
                            HCell(h.Cell(), _isEnglish ? "Void Qty" : "الكمية الملغاة");
                            HCell(h.Cell(), _isEnglish ? "Unit Price" : "سعر الوحدة");
                            HCell(h.Cell(), _isEnglish ? "Void Total" : "قيمة الملغي");
                            HCell(h.Cell(), _isEnglish ? "Item Reason" : "سبب حذف الصنف");
                            HCell(h.Cell(), _isEnglish ? "Voided By" : "حذف بواسطة");
                        });

                        foreach (var itm in items)
                        {
                            decimal iQty = (itm.VoidAmount ?? 0) > 0 ? itm.VoidAmount.Value : itm.Quantity;
                            decimal iPrice = itm.Price ?? 0;
                            decimal iTotal = (itm.TotalVoidAmount ?? 0) > 0 ? itm.TotalVoidAmount.Value : (iQty * iPrice);

                            void DCell(IContainer c, string val, bool bold = false, Color? clr = null)
                            {
                                var txt = c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(val).FontSize(7);
                                if (bold) txt.Bold();
                                if (clr.HasValue) txt.FontColor(clr.Value);
                            }

                            DCell(t.Cell(), itm.NameAr ?? itm.Name ?? "صنف", bold: true);
                            DCell(t.Cell(), $"{iQty:0.##}");
                            DCell(t.Cell(), $"{iPrice:0.##}");
                            DCell(t.Cell(), $"{iTotal:0.##} {Currency}", bold: true, clr: Colors.Red.Darken3);
                            DCell(t.Cell(), !string.IsNullOrWhiteSpace(itm.VoidReason) ? itm.VoidReason : (o.VoidReason ?? "-"));
                            DCell(t.Cell(), !string.IsNullOrWhiteSpace(itm.VoidByName) ? itm.VoidByName : (o.VoidByName ?? "-"));
                        }
                    });
                }
            });
        }

        // Grand Summary Bar
        decimal totalGrandVoid = uniqueOrders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? o.GrandTotal ?? 0 : 0));
        col.Item().PaddingTop(4).Background(Colors.Red.Darken4).Padding(6).Row(gr =>
        {
            gr.RelativeItem().Text(_isEnglish ? $"Total Voided for {uniqueOrders.Count} orders" : $"إجمالي مبالغ الإلغاء لـ {uniqueOrders.Count} طلب")
                .FontSize(9).Bold().FontColor(Colors.White);
            gr.AutoItem().Text($"{totalGrandVoid:0.##} {Currency}")
                .FontSize(11).Bold().FontColor(Colors.White);
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
        var uniqueOrders = _orders.GroupBy(o => o.OrderId).Select(g => g.First()).ToList();
        decimal totalVoided = uniqueOrders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? o.GrandTotal ?? 0 : 0));

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Void Orders Count:" : "عدد الطلبات الملغاة:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(uniqueOrders.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Voided Amount:" : "إجمالي المبلغ الملغي:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{totalVoided:0.##} {Currency}").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var o in uniqueOrders)
        {
            bool isFullVoid = o.OrderState == "Voided";
            decimal voidedAmt = (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (isFullVoid ? (o.GrandTotal ?? 0) : 0);
            decimal beforeAmt = isFullVoid ? (voidedAmt > 0 ? voidedAmt : (o.GrandTotal ?? o.SubTotal ?? 0)) : ((o.GrandTotal ?? 0) + voidedAmt);
            decimal afterAmt = isFullVoid ? 0 : (o.GrandTotal ?? 0);

            var items = (o.OrderDetails ?? new List<TableItem>())
                .Where(i => i.IsVoided || (i.VoidAmount ?? 0) > 0 || (i.TotalVoidAmount ?? 0) > 0)
                .ToList();

            if (!items.Any() && isFullVoid)
            {
                items = (o.OrderDetails ?? new List<TableItem>()).ToList();
            }

            col.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Text($"#{o.OrderId} {o.OrderType} ({o.OrderDate:HH:mm})").FontSize(8).Bold();
                r.AutoItem().Text(isFullVoid ? "إلغاء كامل" : "إلغاء جزئي").FontSize(7).Bold().FontColor(Colors.Red.Darken3);
            });

            col.Item().Row(r =>
            {
                r.RelativeItem().Text($"الكاشير: {o.CashierName ?? o.StaffName ?? "-"} | حذف: {o.VoidByName ?? "-"}").FontSize(7).FontColor(Colors.Grey.Darken3);
            });

            if (!string.IsNullOrWhiteSpace(o.VoidReason))
            {
                col.Item().Text($"السبب: {o.VoidReason}").FontSize(7).FontColor(Colors.Grey.Darken4);
            }

            col.Item().Row(r =>
            {
                r.RelativeItem().Text($"قبل: {beforeAmt:0.##} | بعد: {afterAmt:0.##}").FontSize(7);
                r.AutoItem().Text($"الملغي: {voidedAmt:0.##} {Currency}").FontSize(8).Bold().FontColor(Colors.Red.Darken4);
            });

            if (items.Any())
            {
                col.Item().PaddingLeft(4).Column(ic =>
                {
                    foreach (var itm in items)
                    {
                        decimal iQty = (itm.VoidAmount ?? 0) > 0 ? itm.VoidAmount.Value : itm.Quantity;
                        decimal iPrice = itm.Price ?? 0;
                        decimal iTotal = (itm.TotalVoidAmount ?? 0) > 0 ? itm.TotalVoidAmount.Value : (iQty * iPrice);

                        ic.Item().Row(ir =>
                        {
                            ir.RelativeItem().Text($"• {iQty:0.##}x {itm.NameAr ?? itm.Name}").FontSize(7);
                            ir.AutoItem().Text($"{iTotal:0.##}").FontSize(7).Bold().FontColor(Colors.Red.Darken3);
                        });

                        string itmReason = !string.IsNullOrWhiteSpace(itm.VoidReason) ? itm.VoidReason : "";
                        string itmBy = !string.IsNullOrWhiteSpace(itm.VoidByName) ? itm.VoidByName : "";
                        if (!string.IsNullOrEmpty(itmReason) || !string.IsNullOrEmpty(itmBy))
                        {
                            ic.Item().PaddingLeft(6).Text($"({itmReason} - {itmBy})").FontSize(6).FontColor(Colors.Grey.Darken2);
                        }
                    }
                });
            }

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}