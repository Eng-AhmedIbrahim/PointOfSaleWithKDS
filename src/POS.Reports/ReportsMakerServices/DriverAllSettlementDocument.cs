using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

/// <summary>
/// Prints a single consolidated receipt showing ALL drivers' settlement summary.
/// Matches the "Driver Bonus" paper receipt format (Name | Count | Service | Bonus).
/// </summary>
public class DriverAllSettlementDocument : IDocument
{
    private readonly List<DriverSettlementDto> _settlements;
    private readonly DateTime _posDate;
    private readonly string _branchName;
    private readonly string _logoPath;
    private readonly ReportPageFormat _format;
    private readonly bool _isArabic;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;

    public DriverAllSettlementDocument(
        List<DriverSettlementDto> settlements,
        DateTime posDate,
        string branchName,
        string logoPath,
        ReportPageFormat format,
        bool isArabic = true,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        _settlements = settlements ?? new List<DriverSettlementDto>();
        _posDate = posDate;
        _branchName = branchName;
        _logoPath = logoPath;
        _format = format;
        _isArabic = isArabic;
        _fromDate = fromDate ?? posDate;
        _toDate = toDate ?? posDate;
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

            var fontSize = _format == ReportPageFormat.Cashier ? 8 : 10;

            var baseStyle = TextStyle.Default
                .FontFamily("Arial", "Noto Sans Arabic")
                .FontSize(fontSize);

            page.DefaultTextStyle(baseStyle);

            if (_isArabic)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(2);

                // ── Branch Name (top) ──────────────────────────────────────
                col.Item().AlignCenter().Text(_branchName)
                    .FontSize(fontSize + 3).Bold();

                // ── Report Title ───────────────────────────────────────────
                col.Item().AlignCenter().Text(_isArabic ? "تسوية الطيارين" : "Driver Bonus")
                    .FontSize(fontSize + 5).Bold();

                col.Item().PaddingVertical(1).LineHorizontal(1f).LineColor(Colors.Black);

                // ── Meta info table ────────────────────────────────────────
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(1.2f);
                        cd.RelativeColumn(1.8f);
                    });

                    void MetaRow(string label, string value)
                    {
                        t.Cell().PaddingVertical(1).Text(label).FontSize(fontSize - 1);
                        t.Cell().PaddingVertical(1).Text(value).FontSize(fontSize - 1).Bold();
                    }

                    MetaRow(_isArabic ? "إسم الفرع" : "Branch",  _branchName);
                    MetaRow(_isArabic ? "تاريخ الطباعة" : "Printed",
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    MetaRow(_isArabic ? "من تاريخ" : "From", _fromDate.ToString("d/M/yyyy"));
                    MetaRow(_isArabic ? "إلي تاريخ" : "To",  _toDate.ToString("d/M/yyyy"));
                });

                col.Item().PaddingVertical(1).LineHorizontal(1f).LineColor(Colors.Black);

                // ── Drivers Table ──────────────────────────────────────────
                if (_settlements.Any())
                {
                    col.Item().Table(t =>
                    {
                        // Columns: Name | Count | Amount | Bonus
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2.5f); // Name
                            cd.RelativeColumn(0.8f); // Count
                            cd.RelativeColumn(1.3f); // Amount
                            cd.RelativeColumn(1.3f); // Bonus
                        });

                        // Header
                        t.Header(h =>
                        {
                            IContainer HdrCell(IContainer c) =>
                                c.Border(0.5f).Background(Colors.Grey.Lighten2)
                                 .PaddingVertical(2).PaddingHorizontal(1);

                            h.Cell().Element(HdrCell).AlignCenter()
                                .Text(_isArabic ? "الإسم" : "Name").Bold().FontSize(fontSize);
                            h.Cell().Element(HdrCell).AlignCenter()
                                .Text(_isArabic ? "العدد" : "Count").Bold().FontSize(fontSize);
                            h.Cell().Element(HdrCell).AlignCenter()
                                .Text(_isArabic ? "خدمة" : "Service").Bold().FontSize(fontSize);
                            h.Cell().Element(HdrCell).AlignCenter()
                                .Text(_isArabic ? "مكافأة" : "Bonus").Bold().FontSize(fontSize);
                        });

                        // Data rows
                        bool even = false;
                        foreach (var s in _settlements)
                        {
                            var bg = even ? Colors.Grey.Lighten5 : Colors.White;
                            even = !even;

                            IContainer DataCell(IContainer c) =>
                                c.Border(0.5f).Background(bg)
                                 .PaddingVertical(2).PaddingHorizontal(1);

                            t.Cell().Element(DataCell).AlignRight()
                                .Text(s.DriverDisplayName ?? s.DriverName).FontSize(fontSize);
                            t.Cell().Element(DataCell).AlignCenter()
                                .Text(s.OrderCount.ToString()).FontSize(fontSize);
                            t.Cell().Element(DataCell).AlignCenter()
                                .Text(s.TotalAmount.ToString("0.###")).FontSize(fontSize);
                            t.Cell().Element(DataCell).AlignCenter()
                                .Text(s.TotalBonus.ToString("0.###")).FontSize(fontSize);
                        }

                        // Totals row
                        IContainer TotalCell(IContainer c) =>
                            c.Border(1f).Background(Colors.Grey.Lighten3)
                             .PaddingVertical(2).PaddingHorizontal(1);

                        t.Cell().Element(TotalCell).AlignCenter()
                            .Text(_isArabic ? "الإجمالي" : "Total").Bold().FontSize(fontSize);
                        t.Cell().Element(TotalCell).AlignCenter()
                            .Text(_settlements.Sum(s => s.OrderCount).ToString()).Bold().FontSize(fontSize);
                        t.Cell().Element(TotalCell).AlignCenter()
                            .Text(_settlements.Sum(s => s.TotalAmount).ToString("0.###")).Bold().FontSize(fontSize);
                        t.Cell().Element(TotalCell).AlignCenter()
                            .Text(_settlements.Sum(s => s.TotalBonus).ToString("0.###")).Bold().FontSize(fontSize);
                    });
                }
                else
                {
                    col.Item().AlignCenter().Text(_isArabic ? "لا توجد بيانات" : "No Data")
                        .FontSize(fontSize).FontColor(Colors.Grey.Medium);
                }

                col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                // Footer
                col.Item().AlignCenter().Text(text =>
                {
                    text.Span(_isArabic ? "طبع في: " : "Printed: ").FontSize(fontSize - 2);
                    text.Span(DateTime.Now.ToString("HH:mm dd-MM-yyyy")).FontSize(fontSize - 2);
                });
            });
        });
    }
}
