using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

public class DriverSettlementDocument : IDocument
{
    private readonly DriverSettlementDto _settlement;
    private readonly DateTime _posDate;
    private readonly string _branchName;
    private readonly string _logoPath;
    private readonly ReportPageFormat _format;
    private readonly bool _isArabic;

    public DriverSettlementDocument(DriverSettlementDto settlement, DateTime posDate, string branchName, string logoPath, ReportPageFormat format, bool isArabic = true)
    {
        _settlement = settlement;
        _posDate = posDate;
        _branchName = branchName;
        _logoPath = logoPath;
        _format = format;
        _isArabic = isArabic;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            if (_format == ReportPageFormat.Cashier)
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

            var baseStyle = TextStyle.Default
                .FontFamily("Arial", "Noto Sans Arabic")
                .FontSize(_format == ReportPageFormat.Cashier ? 9 : 11);

            page.DefaultTextStyle(baseStyle);

            if (_isArabic)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(2);

                // 1. Header Information (Centered)
                col.Item().AlignCenter().Text(text => text.Span(_isArabic ? "تسوية الطيار" : "Driver Settlement").FontSize(18).Bold().FontColor(Colors.Black));
                col.Item().AlignCenter().Text(_settlement.DriverDisplayName ?? _settlement.DriverName).FontSize(14);
                col.Item().AlignCenter().Text(text => text.Span(_posDate.ToString("yyyy-MM-dd")).FontSize(12).FontColor(Colors.Grey.Medium));

                // Thick solid line separator
                col.Item().PaddingVertical(2).LineHorizontal(1.5f).LineColor(Colors.Black);

                // 2. Summary Section (Label Right, Value Left)
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(1); });
                    
                    // Row: Orders Count
                    t.Cell().Element(SummaryStyle).AlignRight().Text(_isArabic ? "عدد الطلبات" : "Orders Count").FontSize(11);
                    t.Cell().Element(SummaryStyle).AlignLeft().Text(text => text.Span(_settlement.OrderCount.ToString()).FontSize(11).Bold());
                    
                    // Row: Total Amount (Green)
                    t.Cell().Element(SummaryStyle).AlignRight().Text(text => text.Span(_isArabic ? "إجمالي المبلغ" : "Total Amount").FontSize(11).FontColor(Colors.Green.Darken2));
                    t.Cell().Element(SummaryStyle).AlignLeft().Text(text => text.Span(_settlement.TotalAmount.ToString("0.###")).FontSize(12).Bold().FontColor(Colors.Green.Medium));
                    
                    // Row: Bonus (Orange)
                    if (_settlement.TotalBonus > 0)
                    {
                        t.Cell().Element(SummaryStyle).AlignRight().Text(text => text.Span(_isArabic ? "الـبـونـص" : "Bonus").FontSize(11).FontColor(Colors.Orange.Medium));
                        t.Cell().Element(SummaryStyle).AlignLeft().Text(text => text.Span(_settlement.TotalBonus.ToString("0.###")).FontSize(12).Bold().FontColor(Colors.Orange.Medium));
                    }
                });

                // Dotted line separator
                col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Black);

                // 3. Table Title "تفاصيل الطلبات"
                col.Item().Background(Colors.Black).PaddingVertical(2).AlignCenter().Text(text => text.Span(_isArabic ? "تـفـاصـيـل الـطـلـبـات" : "Order Details").Bold().FontSize(12).FontColor(Colors.White));

                // 4. Detailed Orders Table
                if (_settlement.Orders != null && _settlement.Orders.Any())
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(0.8f); // ID
                            cd.RelativeColumn(1.2f); // Time
                            cd.RelativeColumn(2.5f); // Customer
                            cd.RelativeColumn(1.3f); // Total
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderStyle).AlignCenter().Text(text => text.Span(_isArabic ? "رقم" : "No").Bold());
                            h.Cell().Element(HeaderStyle).AlignCenter().Text(text => text.Span(_isArabic ? "الوقت" : "Time").Bold());
                            h.Cell().Element(HeaderStyle).AlignCenter().Text(text => text.Span(_isArabic ? "العميل" : "Customer").Bold());
                            h.Cell().Element(HeaderStyle).AlignRight().Text(text => text.Span(_isArabic ? "الإجمالي" : "Total").Bold());
                        });

                        foreach (var o in _settlement.Orders)
                        {
                            t.Cell().Element(DetailStyle).AlignCenter().Text($"#{o.OrderId}");
                            t.Cell().Element(DetailStyle).AlignCenter().Text(o.OrderDate?.ToString("HH:mm") ?? "--:--");
                            t.Cell().Element(DetailStyle).AlignCenter().Text(o.CustomerName ?? "—").FontSize(9);
                            t.Cell().Element(DetailStyle).AlignRight().Text(o.GrandTotal?.ToString("0.###"));
                        }

                        // Grand Total Row
                        t.Cell().ColumnSpan(3).Border(0.5f).Background(Colors.Grey.Lighten4).AlignCenter().Text(text => text.Span(_isArabic ? "الإجـمـالـي" : "Grand Total").Bold().FontSize(12));
                        t.Cell().Border(0.5f).Background(Colors.Grey.Lighten4).AlignRight().Text(text => text.Span(_settlement.Orders.Sum(x => x.GrandTotal ?? 0).ToString("0.###")).Bold().FontSize(12));
                    });
                }

                // Dotted line separator
                col.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor(Colors.Black);

                // 5. Footer
                col.Item().AlignCenter().Text(text => {
                    text.Span(_isArabic ? "طـبـع فـي: " : "Printed at: ").FontSize(9);
                    text.Span($"{DateTime.Now:HH:mm dd-MM-yyyy}").FontSize(9);
                });
            });
        });
    }

    private IContainer HeaderStyle(IContainer container) => container.Border(0.5f).Background(Colors.Grey.Lighten3).PaddingVertical(1).PaddingHorizontal(1);
    private IContainer DetailStyle(IContainer container) => container.Border(0.5f).PaddingVertical(1).PaddingHorizontal(1);
    private IContainer SummaryStyle(IContainer container) => container.Border(0.5f).PaddingVertical(1).PaddingHorizontal(2);

    private void AddRow(TableDescriptor t, string lbl, string val, bool bold = false)
    {
        t.Cell().PaddingVertical(1).Text(text => {
            var span = text.Span(lbl).FontSize(10);
            if (bold) span.Bold();
        });
        
        t.Cell().PaddingVertical(1).Text(text => {
            var span = text.Span(val).FontSize(10);
            if (bold) span.Bold();
        });
    }

    private void AddSummaryCell(TableDescriptor t, string lbl, string val, string color = null)
    {
        t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Column(c =>
        {
            c.Item().AlignCenter().Text(text => text.Span(lbl).FontSize(9).FontColor(Colors.Grey.Darken1));
            c.Item().AlignCenter().Text(text => text.Span(val).FontSize(12).Bold().FontColor(color ?? Colors.Black));
        });
    }
}
