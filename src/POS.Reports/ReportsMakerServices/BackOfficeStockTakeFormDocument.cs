using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeStockTakeFormDocument : IDocument
{
    private readonly List<dynamic> _inventory;
    private readonly string _branchName;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeStockTakeFormDocument(IEnumerable<dynamic> inventory, string branchName, string lang = "ar", bool isThermal = false)
    {
        _inventory   = inventory?.ToList() ?? new List<dynamic>();
        _branchName  = branchName;
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
                    ComposeA4Instructions(col);
                    ComposeA4FormTable(col);
                    ComposeA4Signatures(col);
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
                    c.Item().Text(_isEnglish ? "Physical Stock Count & Audit Sheet" : "استمارة الجرد الفعلي الدوري للمخازن").FontSize(10).FontColor(Colors.BlueGrey.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"Date: {DateTime.Now:dd/MM/yyyy}"
                        : $"تاريخ الجرد: {DateTime.Now:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("HH:mm")).FontSize(8).FontColor(Colors.BlueGrey.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Physical Stock Take Form" : "استمارة الجرد الفعلي")
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

    private void ComposeA4Instructions(ColumnDescriptor col)
    {
        col.Item().Background(Colors.Amber.Lighten5).Border(1).BorderColor(Colors.Amber.Lighten2).Padding(6).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(_isEnglish ? "INSTRUCTIONS FOR INVENTORY COMMITTEE:" : "تعليمات لجنة الجرد الفعلي:")
                    .FontSize(9).Bold().FontColor(Colors.Orange.Darken4);
                c.Item().Text(_isEnglish
                    ? "1. Count all physical quantities carefully.  2. Write actual count in the 'Actual Count' column.  3. Both committee members and branch manager must sign below."
                    : "١. يرجى عد ومطابقة الكميات الفعلية بدقة.  ٢. تسجيل الكمية الفعلية يدوياً في خانة 'العد الفعلي'.  ٣. توقيع أمين المخزن ومدير الفرع ولجنة الجرد.")
                    .FontSize(8).FontColor(Colors.Grey.Darken3);
            });
            row.ConstantItem(100).AlignCenter().Column(c =>
            {
                c.Item().Text(_isEnglish ? "Items to Audit" : "عدد الأصناف").FontSize(8).FontColor(Colors.Grey.Darken2);
                c.Item().Text(_inventory.Count.ToString()).FontSize(14).Bold().FontColor(Colors.Orange.Darken4);
            });
        });
    }

    private void ComposeA4FormTable(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(35);   // Code
                cd.RelativeColumn(3.5f); // Item Name
                cd.RelativeColumn(2);    // Category
                cd.RelativeColumn(1.2f); // Unit
                cd.RelativeColumn(1.8f); // System Qty
                cd.RelativeColumn(2.2f); // Actual Count
                cd.RelativeColumn(2);    // Variance (+/-)
            });

            t.Header(h =>
            {
                void Hdr(IContainer cell, string text) =>
                    cell.Background(Colors.BlueGrey.Darken4).Padding(4).AlignCenter()
                        .Text(text).FontSize(8.5f).Bold().FontColor(Colors.White);

                Hdr(h.Cell(), _isEnglish ? "Code" : "الكود");
                Hdr(h.Cell(), _isEnglish ? "Item Name" : "اسم الصنف");
                Hdr(h.Cell(), _isEnglish ? "Category" : "الفئة");
                Hdr(h.Cell(), _isEnglish ? "Unit" : "الوحدة");
                Hdr(h.Cell(), _isEnglish ? "System Qty" : "رصيد النظام");
                Hdr(h.Cell(), _isEnglish ? "Actual Count [ ]" : "العد الفعلي [ ]");
                Hdr(h.Cell(), _isEnglish ? "Variance" : "الفارق (+/-)");
            });

            for (int i = 0; i < _inventory.Count; i++)
            {
                var inv = _inventory[i];
                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                void DataCell(IContainer cell, string text, bool bold = false)
                {
                    var txt = cell.Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                        .Padding(3).AlignCenter().Text(text).FontSize(8.5f);
                    if (bold) txt.Bold();
                }

                string idStr = inv.Id?.ToString() ?? "-";
                string nameStr = inv.ItemName?.ToString() ?? "-";
                string catStr = inv.Category?.ToString() ?? "-";
                string unitStr = inv.Unit?.ToString() ?? "-";
                string sysQtyStr = inv.SystemQty?.ToString("0.###") ?? "0";

                DataCell(t.Cell(), idStr);
                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(3).AlignRight().Text(nameStr).FontSize(8.5f).Bold();
                DataCell(t.Cell(), catStr);
                DataCell(t.Cell(), unitStr);
                DataCell(t.Cell(), sysQtyStr, true);

                // Big empty box for manual pen entry
                t.Cell().Background(bgColor).Border(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(10);
                // Box for variance
                t.Cell().Background(bgColor).Border(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10);
            }
        });
    }

    private void ComposeA4Signatures(ColumnDescriptor col)
    {
        col.Item().PaddingTop(15).Row(row =>
        {
            SignatureBox(row.RelativeItem(), _isEnglish ? "Storekeeper Signature" : "أمين المخزن المسئول");
            row.ConstantItem(20);
            SignatureBox(row.RelativeItem(), _isEnglish ? "Audit Committee Head" : "رئيس لجنة الجرد");
            row.ConstantItem(20);
            SignatureBox(row.RelativeItem(), _isEnglish ? "Branch Manager" : "مدير الفرع / الاعتماد");
        });
    }

    private static void SignatureBox(IContainer c, string title)
    {
        c.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(col =>
        {
            col.Item().AlignCenter().Text(title).FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
            col.Item().PaddingTop(30).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            col.Item().PaddingTop(3).AlignCenter().Text("الاسم والتوقيع: ............................").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Stock Take Form" : "استمارة الجرد").FontSize(10).Bold();
        col.Item().AlignCenter().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Darken2);
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Total Items:" : "عدد الأصناف:").FontSize(8);
            row.ConstantItem(50).AlignRight().Text(_inventory.Count.ToString()).FontSize(8).Bold();
        });
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

        foreach (var inv in _inventory)
        {
            string name = inv.ItemName?.ToString() ?? "-";
            string sys = inv.SystemQty?.ToString("0.##") ?? "0";
            string unit = inv.Unit?.ToString() ?? "";

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(name).FontSize(8);
                row.ConstantItem(75).AlignRight().Text($"[   ]  ({sys} {unit})").FontSize(8).Bold();
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
