using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeLowStockReportDocument : IDocument
{
    private readonly List<dynamic> _inventory;
    private readonly string _branchName;
    private readonly DateTime _reportDate;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeLowStockReportDocument(IEnumerable<dynamic> inventory, string branchName, DateTime reportDate, string lang = "ar", bool isThermal = false)
    {
        _inventory   = inventory?.ToList() ?? new List<dynamic>();
        _branchName  = branchName;
        _reportDate  = reportDate;
        _isEnglish   = (lang ?? "ar").ToLower() == "en";
        _isThermal   = isThermal;
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
                    ComposeA4InventoryTable(col);
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
                    c.Item().Text(_isEnglish ? "Low Stock & Reorder Alert Report" : "تقرير تنبيهات نواقص المخزن وإعادة الطلب").FontSize(10).FontColor(Colors.Red.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"Date: {_reportDate:dd/MM/yyyy}"
                        : $"التاريخ: {_reportDate:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Red.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Low Stock Alert Report" : "تقرير تنبيهات نواقص المخزن")
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
        int totalItems = _inventory.Count;
        int zeroStock = _inventory.Count(i => (decimal)i.CurrentQty <= 0);
        int lowStock = _inventory.Count(i => (decimal)i.CurrentQty > 0);

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Alert Items" : "إجمالي أصناف النواقص",
                $"{totalItems}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Zero Stock (Critical)" : "أصناف رصيدها صفر (حرج)",
                $"{zeroStock}", Colors.Red.Darken3, Colors.Red.Lighten4);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Below Minimum" : "أصناف أقل من الحد الأدنى",
                $"{lowStock}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
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

    private void ComposeA4InventoryTable(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // #
                cd.RelativeColumn(4);    // Item Name
                cd.RelativeColumn(2);    // Current Stock
                cd.RelativeColumn(2);    // Min Level
                cd.RelativeColumn(2);    // Shortage / Deficit
                cd.RelativeColumn(1.5f); // Unit
                cd.RelativeColumn(2);    // Priority
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.Red.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), "#");
                Hdr(h.Cell(), _isEnglish ? "Item Name" : "اسم الصنف");
                Hdr(h.Cell(), _isEnglish ? "Current Qty" : "الرصيد الحالي");
                Hdr(h.Cell(), _isEnglish ? "Min Level" : "الحد الأدنى");
                Hdr(h.Cell(), _isEnglish ? "Shortage" : "العجز المطلوب");
                Hdr(h.Cell(), _isEnglish ? "Unit" : "الوحدة");
                Hdr(h.Cell(), _isEnglish ? "Priority" : "درجة الأهمية");
            });

            for (int i = 0; i < _inventory.Count; i++)
            {
                var inv = _inventory[i];
                decimal current = (decimal)inv.CurrentQty;
                decimal min = (decimal)inv.MinQty;
                decimal shortage = Math.Max(0, min - current);
                bool isZero = current <= 0;
                var bgColor = isZero ? Colors.Red.Lighten5 : (i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5);

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

                DataCellColored(t.Cell(), $"{i + 1}", Colors.Red.Darken3, true);
                DataCell(t.Cell(), (string)inv.ItemName, true);
                DataCellColored(t.Cell(), $"{current:0.###}", isZero ? Colors.Red.Darken4 : Colors.Orange.Darken3, true);
                DataCell(t.Cell(), $"{min:0.###}");
                DataCellColored(t.Cell(), $"{shortage:0.###}", Colors.Red.Darken3, true);
                DataCell(t.Cell(), (string)inv.Unit);
                DataCellColored(t.Cell(), isZero ? (_isEnglish ? "CRITICAL" : "عاجل جداً") : (_isEnglish ? "High" : "مهم"), isZero ? Colors.Red.Darken4 : Colors.Orange.Darken3, true);
            }
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Low Stock Alert" : "نواقص المخزن").FontSize(10).Bold();
        col.Item().AlignCenter().Text(_reportDate.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Shortage Items:" : "الأصناف الناقصة:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(_inventory.Count.ToString()).FontSize(8).Bold().FontColor(Colors.Red.Darken2);
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var inv in _inventory)
        {
            decimal cur = (decimal)inv.CurrentQty;
            decimal min = (decimal)inv.MinQty;
            string unit = (string)inv.Unit;

            col.Item().Row(row =>
            {
                row.RelativeItem().Text((string)inv.ItemName).FontSize(8);
                row.ConstantItem(65).AlignRight().Text($"{cur:0.##} / {min:0.##} {unit}").FontSize(8).Bold()
                    .FontColor(cur <= 0 ? Colors.Red.Darken3 : Colors.Orange.Darken3);
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
