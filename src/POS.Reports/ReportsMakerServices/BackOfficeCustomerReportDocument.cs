using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class CustomerReportItem
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? LastOrderDate { get; set; }
}


public class BackOfficeCustomerReportDocument : IDocument
{
    private readonly List<CustomerReportItem> _customers;
    private readonly string _branchName;
    private readonly string _reportTitle;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeCustomerReportDocument(
        List<CustomerReportItem> customers,
        string branchName,
        string reportTitle,
        DateTime fromDate,
        DateTime toDate,
        bool isThermal,
        string? language)
    {
        _customers   = customers ?? new List<CustomerReportItem>();
        _branchName  = branchName;
        _reportTitle = reportTitle;
        _fromDate    = fromDate;
        _toDate      = toDate;
        _isThermal   = isThermal;
        _isEnglish   = (language ?? "ar").ToLower() == "en";
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
                    ComposeA4Table(col);
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
                    c.Item().Text(_reportTitle).FontSize(10).FontColor(Colors.Indigo.Lighten4);
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
        int totalCustomers = _customers.Count;
        decimal totalSpent = _customers.Sum(c => c.TotalAmount);
        int totalOrders = _customers.Sum(c => c.TotalOrders);
        decimal avgSpent = totalCustomers > 0 ? totalSpent / totalCustomers : 0;
        var topCust = _customers.OrderByDescending(c => c.TotalAmount).FirstOrDefault();

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Customers" : "إجمالي العملاء",
                $"{totalCustomers}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Orders" : "إجمالي الطلبات",
                $"{totalOrders}", Colors.Green.Darken4, Colors.Green.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Revenue" : "إجمالي المشتريات",
                $"{totalSpent:0.##} {Currency}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg / Customer" : "متوسط العميل",
                $"{avgSpent:0.##} {Currency}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Top Customer" : "أعلى عميل",
                topCust?.Name ?? "-", Colors.Purple.Darken4, Colors.Purple.Lighten5);
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

    private void ComposeA4Table(ColumnDescriptor col)
    {
        var ranked = _customers.OrderByDescending(c => c.TotalAmount).ToList();

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // Rank
                cd.RelativeColumn(3);    // Name
                cd.RelativeColumn(2);    // Phone
                cd.RelativeColumn(3);    // Address
                cd.RelativeColumn(1.5f); // Orders
                cd.RelativeColumn(2);    // Total Amount
                cd.RelativeColumn(2);    // Last Order
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Indigo.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Customer" : "العميل");
                Hdr(h.Cell(), _isEnglish ? "Phone" : "الهاتف");
                Hdr(h.Cell(), _isEnglish ? "Address" : "العنوان");
                Hdr(h.Cell(), _isEnglish ? "Orders" : "الطلبات");
                Hdr(h.Cell(), _isEnglish ? "Total Spent" : "إجمالي المشتريات");
                Hdr(h.Cell(), _isEnglish ? "Last Order" : "آخر طلب");
            });

            for (int i = 0; i < ranked.Count; i++)
            {
                var cust = ranked[i];
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
                DataCellColored(t.Cell(), cust.Name ?? "-", Colors.Indigo.Darken4, true);
                DataCell(t.Cell(), cust.Phone ?? "-");
                DataCell(t.Cell(), cust.Address ?? "-");
                DataCellColored(t.Cell(), cust.TotalOrders.ToString(), Colors.Green.Darken4, true);
                DataCellColored(t.Cell(), $"{cust.TotalAmount:0.##} {Currency}", Colors.Blue.Darken4, true);
                DataCell(t.Cell(), cust.LastOrderDate?.ToString("dd/MM/yyyy") ?? "-");
            }

            decimal grandSpent = ranked.Sum(c => c.TotalAmount);
            int grandOrders = ranked.Sum(c => c.TotalOrders);

            t.Cell().ColumnSpan(4).Background(Colors.Indigo.Darken4).Padding(6).AlignRight()
                .Text(_isEnglish ? "Grand Total" : "الإجمالي الكلي")
                .FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Indigo.Darken4).Padding(6).AlignCenter()
                .Text(grandOrders.ToString()).FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Indigo.Darken4).Padding(6).AlignCenter()
                .Text($"{grandSpent:0.##} {Currency}").FontSize(10).Bold().FontColor(Colors.White);
            t.Cell().Background(Colors.Indigo.Darken4).Padding(6);
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
        var ranked = _customers.OrderByDescending(c => c.TotalAmount).ToList();
        decimal totalSpent = ranked.Sum(c => c.TotalAmount);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Customers Count:" : "عدد العملاء:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(ranked.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Spent:" : "إجمالي المشتريات:").FontSize(8);
            row.ConstantItem(70).AlignRight().Text($"{totalSpent:0.##} {Currency}").FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        int rank = 1;
        foreach (var cust in ranked.Take(25))
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(16).Text($"{rank}.").FontSize(8).FontColor(Colors.Grey.Darken2);
                row.RelativeItem().Text($"{cust.Name} ({cust.TotalOrders})").FontSize(8);
                row.ConstantItem(60).AlignRight().Text($"{cust.TotalAmount:0.##}").FontSize(8).Bold();
            });
            rank++;
        }

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "TOTAL" : "الإجمالي").FontSize(9).Bold();
            row.ConstantItem(70).AlignRight().Text($"{totalSpent:0.##} {Currency}").FontSize(9).Bold();
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}

