using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeStockMovementDocument : IDocument
{
    private readonly List<dynamic> _transactions;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeStockMovementDocument(IEnumerable<dynamic> transactions, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar", bool isThermal = false)
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
            col.Item().Background(Colors.Indigo.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Detailed Stock Movement Log" : "سجل حركات الأصناف المخزنية التفصيلي").FontSize(10).FontColor(Colors.Indigo.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Indigo.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Stock Movement Report" : "تقرير حركة المخزن")
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
        int inbound = _transactions.Count(t => (decimal)t.QtyChange > 0);
        int outbound = _transactions.Count(t => (decimal)t.QtyChange < 0);
        int uniqueItems = _transactions.Select(t => (string)t.ItemName).Distinct().Count();

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Movements" : "إجمالي الحركات",
                $"{totalEvents}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Inbound (+)" : "حركات إضافة (+)",
                $"{inbound}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Outbound (-)" : "حركات صرف / هالك (-)",
                $"{outbound}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Moved Items" : "الأصناف المتحركة",
                $"{uniqueItems}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
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
                cd.RelativeColumn(2);    // Type
                cd.RelativeColumn(1.8f); // Change
                cd.RelativeColumn(1.8f); // Result Qty
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Indigo.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Item" : "الصنف");
                Hdr(h.Cell(), _isEnglish ? "Date & Time" : "التاريخ والوقت");
                Hdr(h.Cell(), _isEnglish ? "Movement Type" : "نوع الحركة");
                Hdr(h.Cell(), _isEnglish ? "Qty Change" : "التغيير");
                Hdr(h.Cell(), _isEnglish ? "New Balance" : "الرصيد بعد");
            });

            for (int i = 0; i < _transactions.Count; i++)
            {
                var trans = _transactions[i];
                decimal change = (decimal)trans.QtyChange;
                decimal result = (decimal)trans.ResultQty;
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

                DataCellColored(t.Cell(), $"{i + 1}", Colors.Indigo.Darken3, true);
                DataCell(t.Cell(), (string)trans.ItemName, true);
                DataCell(t.Cell(), (string)trans.TimeDisplay);
                DataCell(t.Cell(), (string)trans.Type);

                string changeStr = change > 0 ? $"+{change:0.###}" : $"{change:0.###}";
                DataCellColored(t.Cell(), changeStr, change > 0 ? Colors.Green.Darken4 : (change < 0 ? Colors.Red.Darken3 : Colors.Black), true);

                DataCellColored(t.Cell(), $"{result:0.###}", Colors.Indigo.Darken4, true);
            }
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Stock Movements" : "حركات المخزن").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Movements Count:" : "عدد الحركات:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(_transactions.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var trans in _transactions)
        {
            decimal change = (decimal)trans.QtyChange;
            decimal result = (decimal)trans.ResultQty;
            string sign = change > 0 ? "+" : "";

            col.Item().Row(row =>
            {
                row.RelativeItem().Text((string)trans.ItemName).FontSize(8);
                row.ConstantItem(70).AlignRight().Text($"{sign}{change:0.##} → {result:0.##}").FontSize(8).Bold()
                    .FontColor(change > 0 ? Colors.Green.Darken3 : Colors.Red.Darken3);
            });
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
