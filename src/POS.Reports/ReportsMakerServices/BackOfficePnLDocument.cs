using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;
using POS.Contract.Dtos.ReportingDtos;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficePnLDocument : IDocument
{
    private readonly SalesSummaryDto _summary;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficePnLDocument(SalesSummaryDto summary, string branchName, DateTime from, DateTime to, bool isThermal = false, string lang = "ar")
    {
        _summary    = summary ?? new SalesSummaryDto();
        _branchName = branchName;
        _fromDate   = from;
        _toDate     = to;
        _isThermal  = isThermal;
        _isEnglish  = (lang ?? "ar").ToLower() == "en";
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
                    ComposeA4PnLStatement(col);
                    ComposeA4DetailedExpenses(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.BlueGrey.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Profit & Loss (P&L) Statement" : "قائمة الأرباح والخسائر التقديرية").FontSize(10).FontColor(Colors.BlueGrey.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.BlueGrey.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Profit & Loss Report" : "تقرير الأرباح والخسائر")
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
        decimal grossSales   = _summary.Overall.TotalSales;
        decimal totalDisc    = _summary.Overall.TotalDiscount;
        decimal netSales     = grossSales - totalDisc;
        decimal totalExpense = _summary.DetailedExpenses?.Sum(e => e.Amount) ?? _summary.Overall.Expenses;
        decimal netProfit    = netSales - totalExpense;

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Gross Revenue" : "إجمالي المبيعات",
                $"{grossSales:0.##} {Currency}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Discounts" : "إجمالي الخصومات",
                $"-{totalDisc:0.##} {Currency}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Net Revenue" : "صافي المبيعات",
                $"{netSales:0.##} {Currency}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Expenses" : "المصروفات",
                $"-{totalExpense:0.##} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Net Profit" : "صافي الربح التقديري",
                $"{netProfit:0.##} {Currency}",
                netProfit >= 0 ? Colors.Green.Darken4 : Colors.Red.Darken4,
                netProfit >= 0 ? Colors.Green.Lighten5 : Colors.Red.Lighten5);
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

    private void ComposeA4PnLStatement(ColumnDescriptor col)
    {
        decimal grossSales   = _summary.Overall.TotalSales;
        decimal totalDisc    = _summary.Overall.TotalDiscount;
        decimal netSales     = grossSales - totalDisc;
        decimal tax          = _summary.Overall.TotalSalesTax;
        decimal dineInService = _summary.DineIn?.Service ?? 0;
        decimal deliveryFees  = _summary.Delivery?.DeliveryFees > 0 ? _summary.Delivery.DeliveryFees : (_summary.Delivery?.Service ?? 0);
        decimal totalExpense = _summary.DetailedExpenses?.Sum(e => e.Amount) ?? _summary.Overall.Expenses;
        decimal netProfit    = netSales - totalExpense;

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(3); // Line description
                cd.RelativeColumn(2); // Amount
            });

            // 1. Revenue Header
            t.Cell().ColumnSpan(2).Background(Colors.BlueGrey.Darken4).Padding(5)
                .Text(_isEnglish ? "1. REVENUE (INCOME)" : "1. الإيرادات والمبيعات")
                .FontSize(9).Bold().FontColor(Colors.White);

            AddRow(t, _isEnglish ? "Gross Sales" : "إجمالي المبيعات", $"{grossSales:0.##} {Currency}", isBold: false);
            AddRowColored(t, _isEnglish ? "Less: Discounts" : "يُخصم: الخصومات والتخفيضات", $"-{totalDisc:0.##} {Currency}", Colors.Orange.Darken3);
            AddRow(t, _isEnglish ? "Tax Collected" : "ضريبة القيمة المضافة", $"{tax:0.##} {Currency}");
            AddRow(t, _isEnglish ? "Dine-In Service Charge" : "خدمة الصالة", $"{dineInService:0.##} {Currency}");
            AddRow(t, _isEnglish ? "Delivery Fees / Service" : "خدمة التوصيل", $"{deliveryFees:0.##} {Currency}");
            AddRowColored(t, _isEnglish ? "NET SALES" : "صافي المبيعات", $"{netSales:0.##} {Currency}", Colors.Green.Darken4, Colors.Green.Lighten5, isBold: true);

            // 2. Expenses Header
            t.Cell().ColumnSpan(2).Background(Colors.BlueGrey.Darken4).Padding(5)
                .Text(_isEnglish ? "2. OPERATING EXPENSES & PAYOUTS" : "2. المصروفات والمدفوعات التشغيلية")
                .FontSize(9).Bold().FontColor(Colors.White);

            if (_summary.DetailedExpenses != null && _summary.DetailedExpenses.Any())
            {
                var grouped = _summary.DetailedExpenses
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? (_isEnglish ? "General" : "عام") : e.Category)
                    .Select(g => new { Cat = g.Key, Amount = g.Sum(x => x.Amount) });

                foreach (var g in grouped)
                {
                    AddRowColored(t, g.Cat, $"-{g.Amount:0.##} {Currency}", Colors.Red.Darken3);
                }
            }
            AddRowColored(t, _isEnglish ? "TOTAL OPERATING EXPENSES" : "إجمالي المصروفات", $"-{totalExpense:0.##} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5, isBold: true);

            // 3. Net Result
            var netBg = netProfit >= 0 ? Colors.Green.Lighten4 : Colors.Red.Lighten4;
            var netColor = netProfit >= 0 ? Colors.Green.Darken4 : Colors.Red.Darken4;

            t.Cell().Background(netBg).Border(1).BorderColor(netColor).Padding(6)
                .Text(_isEnglish ? "NET ESTIMATED PROFIT / (LOSS)" : "صافي الربح / (الخسارة) التقديري")
                .FontSize(11).Bold().FontColor(netColor);
            t.Cell().Background(netBg).Border(1).BorderColor(netColor).Padding(6).AlignCenter()
                .Text($"{netProfit:0.##} {Currency}").FontSize(11).Bold().FontColor(netColor);
        });
    }

    private static void AddRow(TableDescriptor t, string label, string value, bool isBold = false)
    {
        var cell1 = t.Cell().Background(Colors.White).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label).FontSize(9);
        if (isBold) cell1.Bold();

        var cell2 = t.Cell().Background(Colors.White).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(value).FontSize(9);
        if (isBold) cell2.Bold();
    }

    private static void AddRowColored(TableDescriptor t, string label, string value, Color color, Color? bg = null, bool isBold = false)
    {
        var rowBg = bg ?? Colors.White;
        var cell1 = t.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label).FontSize(9);
        if (isBold) cell1.Bold();

        var cell2 = t.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(value).FontSize(9).FontColor(color);
        if (isBold) cell2.Bold();
    }

    private void ComposeA4DetailedExpenses(ColumnDescriptor col)
    {
        if (_summary.DetailedExpenses == null || !_summary.DetailedExpenses.Any()) return;

        col.Item().PaddingTop(10).Text(_isEnglish ? "Detailed Expenses Log" : "سجل المصروفات التفصيلي")
            .FontSize(11).Bold().FontColor(Colors.BlueGrey.Darken3);

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.8f); // Date
                cd.RelativeColumn(2);    // Category
                cd.RelativeColumn(2.5f); // By
                cd.RelativeColumn(3.5f); // Description
                cd.RelativeColumn(2);    // Amount
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter()
                        .Text(text).FontSize(8).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), _isEnglish ? "Date" : "التاريخ");
                Hdr(h.Cell(), _isEnglish ? "Category" : "الفئة");
                Hdr(h.Cell(), _isEnglish ? "Responsible" : "المسؤول");
                Hdr(h.Cell(), _isEnglish ? "Description" : "البيان");
                Hdr(h.Cell(), _isEnglish ? "Amount" : "المبلغ");
            });

            for (int i = 0; i < _summary.DetailedExpenses.Count; i++)
            {
                var e = _summary.DetailedExpenses[i];
                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                void DC(IContainer cell, string text, bool bold = false)
                {
                    var txt = cell.Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                        .Padding(3).AlignCenter().Text(text).FontSize(8);
                    if (bold) txt.Bold();
                }

                DC(t.Cell(), e.Date.ToString("dd/MM/yyyy HH:mm"));
                DC(t.Cell(), e.Category ?? "-");
                DC(t.Cell(), e.CreatedByName ?? "-");
                DC(t.Cell(), e.Description ?? "-");
                DC(t.Cell(), $"{e.Amount:0.##} {Currency}", true);
            }

            decimal totalExp = _summary.DetailedExpenses.Sum(e => e.Amount);
            t.Cell().ColumnSpan(4).Background(Colors.BlueGrey.Darken4).Padding(5).AlignRight()
                .Text(_isEnglish ? "Total Expenses" : "إجمالي المصروفات")
                .FontSize(9).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.BlueGrey.Darken4).Padding(5).AlignCenter()
                .Text($"{totalExp:0.##} {Currency}").FontSize(9).Bold().FontColor(Colors.White);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Profit & Loss (P&L)" : "الأرباح والخسائر").FontSize(10).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        decimal grossSales   = _summary.Overall.TotalSales;
        decimal totalDisc    = _summary.Overall.TotalDiscount;
        decimal netSales     = grossSales - totalDisc;
        decimal totalExpense = _summary.DetailedExpenses?.Sum(e => e.Amount) ?? _summary.Overall.Expenses;
        decimal netProfit    = netSales - totalExpense;

        void Row(string label, string val, bool bold = false)
        {
            col.Item().Row(r =>
            {
                var t1 = r.RelativeItem().Text(label).FontSize(8);
                if (bold) t1.Bold();
                var t2 = r.ConstantItem(70).AlignRight().Text(val).FontSize(8);
                if (bold) t2.Bold();
            });
        }

        void RowColored(string label, string val, Color color, bool bold = false)
        {
            col.Item().Row(r =>
            {
                var t1 = r.RelativeItem().Text(label).FontSize(8);
                if (bold) t1.Bold();
                var t2 = r.ConstantItem(70).AlignRight().Text(val).FontSize(8).FontColor(color);
                if (bold) t2.Bold();
            });
        }

        Row(_isEnglish ? "Gross Sales:" : "إجمالي المبيعات:", $"{grossSales:0.##} {Currency}");
        RowColored(_isEnglish ? "Discounts:" : "الخصومات:", $"-{totalDisc:0.##} {Currency}", Colors.Orange.Darken3);
        Row(_isEnglish ? "Net Sales:" : "صافي المبيعات:", $"{netSales:0.##} {Currency}", bold: true);
        RowColored(_isEnglish ? "Expenses:" : "المصروفات:", $"-{totalExpense:0.##} {Currency}", Colors.Red.Darken3);
        
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        RowColored(_isEnglish ? "NET PROFIT:" : "صافي الربح:", $"{netProfit:0.##} {Currency}", netProfit >= 0 ? Colors.Green.Darken3 : Colors.Red.Darken3, bold: true);
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
