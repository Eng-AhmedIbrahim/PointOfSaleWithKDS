using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeWasteReportDocument : IDocument
{
    private readonly List<dynamic> _transactions;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeWasteReportDocument(IEnumerable<dynamic> transactions, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar", bool isThermal = false)
    {
        _transactions = transactions?.ToList() ?? new List<dynamic>();
        _branchName   = branchName;
        _fromDate     = fromDate;
        _toDate       = toDate;
        _isEnglish    = (lang ?? "ar").ToLower() == "en";
        _isThermal    = isThermal;
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
                    ComposeA4TransactionsTable(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Brown.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Waste & Spoilage Report" : "تقرير الهوالك والتوالف المخزنية").FontSize(10).FontColor(Colors.Brown.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Brown.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Waste & Spoilage Report" : "تقرير الهوالك والتوالف")
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
        int totalEvents = _transactions.Count;
        decimal totalQty = _transactions.Sum(t => Math.Abs((decimal)t.QtyChange));
        int uniqueItems = _transactions.Select(t => (string)t.ItemName).Distinct().Count();

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Waste Events" : "عدد مرات الهالك",
                $"{totalEvents}", Colors.Brown.Darken4, Colors.Brown.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Wasted Qty" : "إجمالي كميات الهالك",
                $"{totalQty:0.###}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Impacted Items" : "الأصناف المتأثرة",
                $"{uniqueItems}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
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

    private void ComposeA4TransactionsTable(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // #
                cd.RelativeColumn(3.5f); // Item
                cd.RelativeColumn(2);    // Date/Time
                cd.RelativeColumn(1.8f); // Qty
                cd.RelativeColumn(3.5f); // Reason
                cd.RelativeColumn(2.5f); // Notes
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Brown.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Item" : "الصنف");
                Hdr(h.Cell(), _isEnglish ? "Date & Time" : "التاريخ والوقت");
                Hdr(h.Cell(), _isEnglish ? "Qty Wasted" : "الكمية التالفة");
                Hdr(h.Cell(), _isEnglish ? "Reason" : "السبب");
                Hdr(h.Cell(), _isEnglish ? "Notes" : "الملاحظات");
            });

            for (int i = 0; i < _transactions.Count; i++)
            {
                var trans = _transactions[i];
                decimal qty = Math.Abs((decimal)trans.QtyChange);
                string reason = (string)(trans.Reason ?? "-");
                string notes = (string)(trans.Notes ?? "-");
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

                DataCellColored(t.Cell(), $"{i + 1}", Colors.Brown.Darken3, true);
                DataCell(t.Cell(), (string)trans.ItemName, true);
                DataCell(t.Cell(), (string)trans.TimeDisplay);
                DataCellColored(t.Cell(), $"{qty:0.###}", Colors.Red.Darken3, true);
                DataCell(t.Cell(), string.IsNullOrEmpty(reason) ? "-" : reason);
                DataCell(t.Cell(), string.IsNullOrEmpty(notes) ? "-" : notes);
            }

            decimal totalQty = _transactions.Sum(tr => Math.Abs((decimal)tr.QtyChange));
            t.Cell().ColumnSpan(3).Background(Colors.Brown.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Total Wasted Units" : "إجمالي الكمية التالفة")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Brown.Darken4).Padding(6).AlignCenter()
                .Text($"{totalQty:0.###}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().ColumnSpan(2).Background(Colors.Brown.Darken4).Padding(6);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Waste Report" : "تقرير الهوالك").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        decimal totalQty = _transactions.Sum(t => Math.Abs((decimal)t.QtyChange));

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Events:" : "عدد العمليات:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(_transactions.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Wasted:" : "إجمالي التالف:").FontSize(8);
            row.ConstantItem(60).AlignRight().Text($"{totalQty:0.##}").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var trans in _transactions)
        {
            decimal qty = Math.Abs((decimal)trans.QtyChange);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text((string)trans.ItemName).FontSize(8);
                row.ConstantItem(50).AlignRight().Text($"{qty:0.##}").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
            });
            string reason = (string)trans.Reason;
            if (!string.IsNullOrEmpty(reason))
            {
                col.Item().PaddingLeft(8).Text($"→ {reason}").FontSize(7).FontColor(Colors.Grey.Darken2);
            }
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
