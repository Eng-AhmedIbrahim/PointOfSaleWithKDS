using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;
using POS.Contract.Dtos.ReportingDtos;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeInventoryReportDocument : IDocument
{
    private readonly List<InventorySummaryDto> _inventory;
    private readonly string _branchName;
    private readonly DateTime _reportDate;
    private readonly string _reportTitle;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeInventoryReportDocument(List<InventorySummaryDto> inventory, string branchName, DateTime reportDate, string title = "تقرير حالة المخزن", string lang = "ar", bool isThermal = false)
    {
        _inventory   = inventory ?? new List<InventorySummaryDto>();
        _branchName  = branchName;
        _reportDate  = reportDate;
        _reportTitle = title ?? (_isEnglish ? "Inventory Status Report" : "تقرير حالة المخزن");
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
            col.Item().Background(Colors.BlueGrey.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_reportTitle).FontSize(10).FontColor(Colors.BlueGrey.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"Date: {_reportDate:dd/MM/yyyy}"
                        : $"التاريخ: {_reportDate:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.BlueGrey.Lighten4);
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
        int totalItems = _inventory.Count;
        int lowStockCount = _inventory.Count(i => i.TrackInventory && i.CurrentQuantity <= i.MinimumQuantity && i.CurrentQuantity > 0);
        int outOfStockCount = _inventory.Count(i => i.TrackInventory && i.CurrentQuantity <= 0);
        int totalCategories = _inventory.Select(i => _isEnglish ? i.CategoryNameEn : i.CategoryNameAr).Distinct().Count();
        decimal totalUnits = _inventory.Sum(i => i.CurrentQuantity);

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Items" : "إجمالي الأصناف",
                $"{totalItems}", Colors.BlueGrey.Darken4, Colors.BlueGrey.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Categories" : "عدد الفئات",
                $"{totalCategories}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Low Stock Items" : "أصناف قريبة من النفاد",
                $"{lowStockCount}", Colors.Orange.Darken4, Colors.Orange.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Out of Stock" : "أصناف نفدت",
                $"{outOfStockCount}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Stock Qty" : "إجمالي الأرصدة",
                $"{totalUnits:0.##}", Colors.Green.Darken4, Colors.Green.Lighten5);
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

    private void ComposeA4InventoryTable(ColumnDescriptor col)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(40);   // ID
                cd.RelativeColumn(3.5f); // Item Name
                cd.RelativeColumn(2);    // Category
                cd.RelativeColumn(1.5f); // Unit
                cd.RelativeColumn(2);    // Current Stock
                cd.RelativeColumn(2);    // Min Stock
                cd.RelativeColumn(2);    // Status
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.BlueGrey.Darken4).Padding(5).AlignCenter()
                        .Text(text).FontSize(9).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), _isEnglish ? "ID" : "الكود");
                Hdr(h.Cell(), _isEnglish ? "Item Name" : "اسم الصنف");
                Hdr(h.Cell(), _isEnglish ? "Category" : "الفئة");
                Hdr(h.Cell(), _isEnglish ? "Unit" : "الوحدة");
                Hdr(h.Cell(), _isEnglish ? "Current Stock" : "الرصيد الحالي");
                Hdr(h.Cell(), _isEnglish ? "Min Level" : "الحد الأدنى");
                Hdr(h.Cell(), _isEnglish ? "Status" : "الحالة");
            });

            int rowIdx = 0;
            foreach (var itm in _inventory)
            {
                bool isOutOfStock = itm.TrackInventory && itm.CurrentQuantity <= 0;
                bool isLow = itm.TrackInventory && !isOutOfStock && itm.CurrentQuantity <= itm.MinimumQuantity;
                
                string itemName = _isEnglish ? (itm.ItemNameEn ?? itm.ItemNameAr) : (itm.ItemNameAr ?? itm.ItemNameEn);
                string catName  = _isEnglish ? (itm.CategoryNameEn ?? itm.CategoryNameAr) : (itm.CategoryNameAr ?? itm.CategoryNameEn);
                string unitName = _isEnglish ? (itm.UnitNameEn ?? itm.UnitNameAr) : (itm.UnitNameAr ?? itm.UnitNameEn);

                var bgColor = isOutOfStock ? Colors.Red.Lighten5 : (isLow ? Colors.Orange.Lighten5 : (rowIdx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5));

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

                DataCell(t.Cell(), itm.Id.ToString());
                DataCell(t.Cell(), itemName ?? "-", true);
                DataCell(t.Cell(), catName ?? "-");
                DataCell(t.Cell(), unitName ?? "-");

                if (isOutOfStock)
                    DataCellColored(t.Cell(), $"{itm.CurrentQuantity:0.###}", Colors.Red.Darken3, true);
                else if (isLow)
                    DataCellColored(t.Cell(), $"{itm.CurrentQuantity:0.###}", Colors.Orange.Darken3, true);
                else
                    DataCellColored(t.Cell(), $"{itm.CurrentQuantity:0.###}", Colors.Green.Darken4, true);

                DataCell(t.Cell(), $"{itm.MinimumQuantity:0.###}");

                if (isOutOfStock)
                    DataCellColored(t.Cell(), _isEnglish ? "Out of Stock" : "منتهي", Colors.Red.Darken3, true);
                else if (isLow)
                    DataCellColored(t.Cell(), _isEnglish ? "Low Stock" : "منخفض", Colors.Orange.Darken3, true);
                else
                    DataCellColored(t.Cell(), _isEnglish ? "Normal" : "متوفر", Colors.Green.Darken3, false);

                rowIdx++;
            }
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_reportTitle).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_reportDate.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        int totalItems = _inventory.Count;
        int lowCount = _inventory.Count(i => i.TrackInventory && i.CurrentQuantity <= i.MinimumQuantity);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Items:" : "إجمالي الأصناف:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(totalItems.ToString()).FontSize(8).Bold();
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Low/Out Stock:" : "أصناف منخفضة:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(lowCount.ToString()).FontSize(8).Bold().FontColor(Colors.Red.Darken2);
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var itm in _inventory)
        {
            bool isLow = itm.TrackInventory && itm.CurrentQuantity <= itm.MinimumQuantity;
            string itemName = _isEnglish ? (itm.ItemNameEn ?? itm.ItemNameAr) : (itm.ItemNameAr ?? itm.ItemNameEn);
            string unitName = _isEnglish ? (itm.UnitNameEn ?? itm.UnitNameAr) : (itm.UnitNameAr ?? itm.UnitNameEn);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(itemName ?? "-").FontSize(8);
                row.ConstantItem(60).AlignRight().Text($"{itm.CurrentQuantity:0.##} {unitName}").FontSize(8).Bold()
                    .FontColor(isLow ? Colors.Red.Darken2 : Colors.Black);
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
