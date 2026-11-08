using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeRecipeReportDocument : IDocument
{
    private readonly List<dynamic> _recipes;
    private readonly string _branchName;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;

    public BackOfficeRecipeReportDocument(IEnumerable<dynamic> recipes, string branchName, string lang = "ar", bool isThermal = false)
    {
        _recipes     = recipes?.ToList() ?? new List<dynamic>();
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
                    ComposeA4KpiCards(col);
                    ComposeA4Recipes(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Teal.Darken4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(14).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Products Recipes (BOM) Report" : "تقرير وصفات وتكاليف تصنيع المنتجات (الريسبي)").FontSize(10).FontColor(Colors.Teal.Lighten4);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.White);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Recipes Report" : "تقرير الوصفات (الريسبي)")
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
        var groups = _recipes.GroupBy(r => (string)r.ProductName).ToList();
        int totalProducts = groups.Count;
        int totalIngredients = _recipes.Select(r => (string)r.IngredientName).Distinct().Count();
        int totalComponents = _recipes.Count;

        col.Item().PaddingBottom(8).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Recipes" : "عدد المنتجات المعرفة",
                $"{totalProducts}", Colors.Teal.Darken4, Colors.Teal.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Unique Ingredients" : "المكونات الخام المستخدمة",
                $"{totalIngredients}", Colors.Indigo.Darken4, Colors.Indigo.Lighten5);
            row.ConstantItem(8);
            KpiCard(row.RelativeItem(), _isEnglish ? "Total BOM Links" : "إجمالي روابط المكونات",
                $"{totalComponents}", Colors.Green.Darken4, Colors.Green.Lighten5);
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

    private void ComposeA4Recipes(ColumnDescriptor col)
    {
        var groups = _recipes.GroupBy(r => (string)r.ProductName).ToList();

        foreach (var g in groups)
        {
            col.Item().PaddingTop(6).Column(rCol =>
            {
                rCol.Item().Background(Colors.Teal.Lighten5).Border(1).BorderColor(Colors.Teal.Lighten2).Padding(4).Row(r =>
                {
                    r.RelativeItem().Text(g.Key).FontSize(10).Bold().FontColor(Colors.Teal.Darken4);
                    r.ConstantItem(120).AlignRight().Text($"{g.Count()} {(_isEnglish ? "ingredients" : "مكونات")}").FontSize(8).FontColor(Colors.Teal.Darken3);
                });

                rCol.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(30);   // #
                        cd.RelativeColumn(5);    // Ingredient Name
                        cd.RelativeColumn(2);    // Qty
                        cd.RelativeColumn(2);    // Unit
                    });

                    t.Header(h =>
                    {
                        void Hdr(IContainer cell, string text) =>
                            cell.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                .Padding(3).AlignCenter().Text(text).FontSize(8).Bold().FontColor(Colors.Grey.Darken4);

                        Hdr(h.Cell(), "#");
                        Hdr(h.Cell(), _isEnglish ? "Ingredient" : "المكون الخام");
                        Hdr(h.Cell(), _isEnglish ? "Quantity" : "الكمية المطلوبة");
                        Hdr(h.Cell(), _isEnglish ? "Unit" : "الوحدة");
                    });

                    int idx = 1;
                    foreach (var ing in g)
                    {
                        var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                        t.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(idx.ToString()).FontSize(8);
                        t.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text((string)ing.IngredientName).FontSize(9).Bold();
                        t.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(((decimal)ing.Quantity).ToString("0.####")).FontSize(9).FontColor(Colors.Teal.Darken3).Bold();
                        t.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text((string)ing.Unit).FontSize(8);
                        idx++;
                    }
                });
            });
        }
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(12).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Recipes Report" : "تقرير الريسبي").FontSize(10).Bold();
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        var groups = _recipes.GroupBy(r => (string)r.ProductName).ToList();

        foreach (var g in groups)
        {
            col.Item().Background(Colors.Grey.Lighten3).Padding(2).Text(g.Key).Bold().FontSize(8);
            foreach (var ing in g)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text($"  • {(string)ing.IngredientName}").FontSize(8);
                    row.ConstantItem(60).AlignRight().Text($"{((decimal)ing.Quantity):0.##} {(string)ing.Unit}").FontSize(8).Bold();
                });
            }
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Black);
        col.Item().AlignCenter().Text($"{(_isEnglish ? "Printed: " : "طُبع: ")}{DateTime.Now:dd/MM/yyyy HH:mm}")
            .FontSize(7).FontColor(Colors.Grey.Darken1);
    }
}
