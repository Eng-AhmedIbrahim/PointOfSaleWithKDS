namespace POS.Reports.ReportsMakerServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

public class DetailedOrdersFullDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly string _logoPath;
    private readonly bool _isArabic;
    private readonly DateTime _selectedDate;

    public DetailedOrdersFullDocument(List<OrderDto> orders, 
        string branchName, 
        string logoPath, 
        DateTime selectedDate, 
        bool isArabic = true)
    {
        _orders = orders;
        _branchName = branchName;
        _logoPath = logoPath;
        _selectedDate = selectedDate;
        _isArabic = isArabic;
    }

    private string Localize(string ar, string en) => _isArabic ? ar : en;

    private string LocalizeOrderType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return "";
        return type.ToLower() switch
        {
            "takeaway" => Localize("تيك أواي", "TakeAway"),
            "dinein" => Localize("صالة", "DineIn"),
            "delivery" => Localize("دليفري", "Delivery"),
            _ => type
        };
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.PageColor(Colors.White);

            var baseStyle = TextStyle.Default
                .FontFamily("Arial", "Noto Sans Arabic")
                .FontSize(10);
            
            page.DefaultTextStyle(baseStyle);

            if (_isArabic)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(10);
                ComposeHeader(col);
                ComposeOrdersList(col);
                ComposeFooter(col);
            });
        });
    }

    private void ComposeHeader(ColumnDescriptor col)
    {
        // Branch Name (Black, Center)
        col.Item().AlignCenter().Text(_branchName).FontSize(14).Bold().FontColor(Colors.Black);
        
        // Report Title (Black, Center)
        col.Item().AlignCenter().Text(_isArabic ? "تقرير المبيعات التفصيلية الكامل" : "Detailed Full Sales Report").FontSize(18).Bold().FontColor(Colors.Black);
        
        // Date
        col.Item().AlignCenter().Text(_isArabic ? $"تاريخ العمل: {_selectedDate:yyyy-MM-dd}" : $"Work Date: {_selectedDate:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken2);

        if (!string.IsNullOrEmpty(_logoPath) && File.Exists(_logoPath))
        {
            col.Item().PaddingTop(5).AlignCenter().MaxHeight(50).Image(_logoPath);
        }
        
        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeOrdersList(ColumnDescriptor col)
    {
        foreach (var order in _orders)
        {
            col.Item().PaddingTop(10).Column(orderCol =>
            {
                // Order Header Table
                orderCol.Item().Table(t => {
                    t.ColumnsDefinition(cd => {
                        cd.RelativeColumn(3); // # ID
                        cd.RelativeColumn(4); // Date/Time
                        cd.RelativeColumn(3); // Type
                    });

                    t.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(1).AlignCenter().Text(_isArabic ? $"طلب رقم: {order.OrderId}" : $"Order #: {order.OrderId}").Bold();
                    t.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(1).AlignCenter().Text(order.OrderDate?.ToString("yyyy-MM-dd HH:mm:ss"));
                    t.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(1).AlignCenter().Text(LocalizeOrderType(order.OrderType)).Bold();
                });

                // Items Table
                if (order.OrderDetails != null && order.OrderDetails.Any())
                {
                    orderCol.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { 
                            cd.RelativeColumn(5); // Item
                            cd.RelativeColumn(1.5f); // Qty
                            cd.RelativeColumn(2); // Price
                            cd.RelativeColumn(2); // Total
                        });

                        t.Header(h => {
                            h.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(1).AlignCenter().Text(Localize("الصنف", "Item")).Bold().FontSize(9);
                            h.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(1).AlignCenter().Text(Localize("عدد", "Qty")).Bold().FontSize(9);
                            h.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(1).AlignCenter().Text(Localize("السعر", "Price")).Bold().FontSize(9);
                            h.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(1).AlignCenter().Text(Localize("الإجمالي", "Total")).Bold().FontSize(9);
                        });

                        foreach (var item in order.OrderDetails)
                        {
                            t.Cell().Border(1).Padding(1).AlignRight().Text(_isArabic ? (item.NameAr ?? item.Name) : item.Name).FontSize(9);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(item.Quantity.ToString("G29")).FontSize(9);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text(item.Price?.ToString("N2")).FontSize(9);
                            t.Cell().Border(1).Padding(1).AlignCenter().Text((item.Quantity * (item.Price ?? 0)).ToString("N2")).Bold().FontSize(9);
                        }
                    });
                }

                // Financials Table
                orderCol.Item().Table(ft => {
                    ft.ColumnsDefinition(cd => {
                        cd.RelativeColumn(3);
                        cd.RelativeColumn(1);
                        cd.RelativeColumn(3);
                        cd.RelativeColumn(1);
                    });

                    ft.Cell().Border(1).Padding(1).AlignRight().Text(Localize("إجمالي فرعي:", "Subtotal:")).FontSize(9);
                    ft.Cell().Border(1).Padding(1).AlignCenter().Text(order.SubTotal?.ToString("N2")).FontSize(9);

                    ft.Cell().Border(1).Padding(1).AlignRight().Text(Localize("الإجمالي الكلي:", "Grand Total:")).Bold().FontSize(9);
                    ft.Cell().Border(1).Padding(1).AlignCenter().Text(order.GrandTotal?.ToString("N2")).Bold().FontSize(9);

                    if ((order.TotalDiscount ?? order.TotalOrderDiscount ?? 0) > 0)
                    {
                        var disc = order.TotalOrderDiscount ?? order.TotalDiscount;
                        ft.Cell().Border(1).Padding(1).AlignRight().Text(Localize("إجمالي الخصم:", "Discount:")).FontColor(Colors.Black).FontSize(9);
                        ft.Cell().Border(1).Padding(1).AlignCenter().Text("-" + disc?.ToString("N2")).FontColor(Colors.Black).FontSize(9);
                        
                        string reason = !string.IsNullOrEmpty(order.DiscountReason) ? $" ({order.DiscountReason})" : "";
                        ft.Cell().ColumnSpan(2).Border(1).Padding(1).AlignCenter().Text(Localize("سبب الخصم: ", "Disc Reason: ") + reason).FontSize(8);
                    }
                });

                if (order.OrderState == "Voided" || !string.IsNullOrEmpty(order.VoidReason))
                {
                    orderCol.Item().Table(vt => {
                        vt.ColumnsDefinition(cd => cd.RelativeColumn());
                        string voidText = Localize("🚫 طلب ملغي", "🚫 VOIDED ORDER");
                        if (!string.IsNullOrEmpty(order.VoidReason)) voidText += " | " + Localize("السبب: ", "Reason: ") + order.VoidReason;
                        if (!string.IsNullOrEmpty(order.VoidByName)) voidText += " | " + Localize("بواسطة: ", "By: ") + order.VoidByName;
                        vt.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(voidText).Bold().FontSize(8).FontColor(Colors.Black);
                    });
                }

                orderCol.Item().PaddingVertical(5).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten4);
            });
        }
    }

    private void ComposeFooter(ColumnDescriptor fcol)
    {
        fcol.Item().PaddingTop(20).AlignCenter().Row(r => {
            r.RelativeItem().Text(Localize("تم الطباعة في: ", "Printed at: ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(8);
            r.RelativeItem().AlignRight().Text(x => {
                x.Span(Localize("صفحة ", "Page ")).FontSize(8);
                x.CurrentPageNumber().FontSize(8);
                x.Span(Localize(" من ", " of ")).FontSize(8);
                x.TotalPages().FontSize(8);
            });
        });
    }
}
