using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.OrderDtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Reports.ReportsMakerServices;

public class BackOfficeVoidSummaryDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly string _branchName;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _reportTitle;
    private readonly bool _isEnglish;
    private readonly bool _isThermal;
    private const string Currency = "ج.م";

    public BackOfficeVoidSummaryDocument(
        List<OrderDto> orders,
        string branchName,
        DateTime fromDate,
        DateTime toDate,
        string title = "تقرير ملخص عمليات الحذف (Void)",
        string lang = "ar",
        bool isThermal = false)
    {
        _orders      = orders ?? new List<OrderDto>();
        _branchName  = branchName;
        _fromDate    = fromDate;
        _toDate      = toDate;
        _reportTitle = title;
        _isThermal   = isThermal;
        _isEnglish   = (lang ?? "ar").ToLower() == "en";
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
                .FontSize(_isThermal ? 8 : 8.5f);
            page.DefaultTextStyle(baseStyle);

            if (!_isEnglish) page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 3 : 5);
                if (_isThermal)
                {
                    ComposeThermalHeader(col);
                    ComposeThermalContent(col);
                    ComposeThermalFooter(col);
                }
                else
                {
                    ComposeA4KpiCards(col);
                    ComposeA4ReasonsAndCashiersBreakdown(col);
                    ComposeA4OrdersSummaryTable(col);
                }
            });
        });
    }

    private void ComposeA4Header(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Red.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_branchName).FontSize(13).Bold().FontColor(Colors.White);
                    c.Item().Text(_isEnglish ? "Void Transactions Summary Report" : _reportTitle)
                        .FontSize(10).FontColor(Colors.Red.Lighten4);
                });
                row.ConstantItem(210).Column(c =>
                {
                    c.Item().AlignRight().Text(_isEnglish
                        ? $"From: {_fromDate:dd/MM/yyyy}  To: {_toDate:dd/MM/yyyy}"
                        : $"الفترة من: {_fromDate:dd/MM/yyyy}  إلى: {_toDate:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                    c.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Red.Lighten4);
                });
            });
        });
    }

    private void ComposeA4Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(_isEnglish ? "Void Summary Report" : "تقرير ملخص عمليات الحذف")
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
        int totalOrders = _orders.Count;
        int fullVoids = _orders.Count(o => o.OrderState == "Voided");
        int partialVoids = totalOrders - fullVoids;

        decimal totalVoidAmount = _orders.Sum(o =>
        {
            if ((o.VoidAmount ?? 0) > 0) return o.VoidAmount.Value;
            if (o.OrderState == "Voided") return o.GrandTotal ?? (o.SubTotal ?? 0);
            return o.OrderDetails?.Where(i => i.IsVoided || (i.VoidAmount ?? 0) > 0)
                .Sum(i => (i.TotalVoidAmount ?? 0) > 0 ? i.TotalVoidAmount.Value : (i.VoidAmount ?? 0) * (i.Price ?? 0)) ?? 0;
        });

        decimal avgVoid = totalOrders > 0 ? totalVoidAmount / totalOrders : 0;

        col.Item().PaddingBottom(4).Row(row =>
        {
            KpiCard(row.RelativeItem(), _isEnglish ? "Total Voided Amount" : "إجمالي المبالغ المحذوفة",
                $"{totalVoidAmount:N2} {Currency}", Colors.Red.Darken4, Colors.Red.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Voided Orders" : "إجمالي الطلبات",
                $"{totalOrders:N0}", Colors.Blue.Darken4, Colors.Blue.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Full Voids" : "إلغاء كلي للطلب",
                $"{fullVoids:N0}", Colors.DeepOrange.Darken4, Colors.DeepOrange.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Partial Voids" : "إلغاء جزئي (أصناف)",
                $"{partialVoids:N0}", Colors.Purple.Darken4, Colors.Purple.Lighten5);
            row.ConstantItem(6);
            KpiCard(row.RelativeItem(), _isEnglish ? "Avg Void / Order" : "متوسط الحذف / طلب",
                $"{avgVoid:N2} {Currency}", Colors.Grey.Darken4, Colors.Grey.Lighten4);
        });
    }

    private static void KpiCard(IContainer c, string label, string value, Color textColor, Color bgColor)
    {
        c.Background(bgColor).Border(1).BorderColor(textColor).Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(7.5f).FontColor(textColor);
            col.Item().Text(value).FontSize(9.5f).Bold().FontColor(textColor);
        });
    }

    private void ComposeA4ReasonsAndCashiersBreakdown(ColumnDescriptor col)
    {
        col.Item().PaddingBottom(6).Row(row =>
        {
            // Table 1: Reasons Breakdown
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "Voids by Reason" : "تحليل أسباب الحذف الأكثر تكراراً")
                    .FontSize(9).Bold().FontColor(Colors.Red.Darken3);

                c.Item().PaddingTop(3).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.5f); // Reason
                        cd.RelativeColumn(1f);   // Count
                        cd.RelativeColumn(1.4f); // Amount
                    });

                    void HCell(IContainer cell, string txt)
                    {
                        cell.Background(Colors.Red.Lighten4).Padding(3).AlignCenter().Text(txt)
                            .FontSize(7.5f).Bold().FontColor(Colors.Red.Darken4);
                    }

                    t.Header(h =>
                    {
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Reason" : "سبب الحذف"));
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Count" : "المرات"));
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Amount" : "القيمة"));
                    });

                    var reasons = _orders
                        .GroupBy(o => !string.IsNullOrWhiteSpace(o.VoidReason) ? o.VoidReason : "غير محدد")
                        .Select(g => new
                        {
                            Reason = g.Key,
                            Count = g.Count(),
                            Amount = g.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.GrandTotal ?? 0))
                        })
                        .OrderByDescending(x => x.Amount)
                        .Take(5)
                        .ToList();

                    int rIdx = 0;
                    foreach (var r in reasons)
                    {
                        var bg = rIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                        void DCell(IContainer cell, string txt, bool bold = false)
                        {
                            var tVal = cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                                .Padding(2.5f).AlignCenter().Text(txt).FontSize(7);
                            if (bold) tVal.Bold();
                        }
                        DCell(t.Cell(), r.Reason, bold: true);
                        DCell(t.Cell(), r.Count.ToString());
                        DCell(t.Cell(), $"{r.Amount:N1}", bold: true);
                        rIdx++;
                    }
                });
            });

            row.ConstantItem(8);

            // Table 2: Cashiers Breakdown
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
            {
                c.Item().Text(_isEnglish ? "Voids by Cashier" : "ملخص المحذوفات حسب الكاشير")
                    .FontSize(9).Bold().FontColor(Colors.Indigo.Darken3);

                c.Item().PaddingTop(3).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.5f); // Cashier
                        cd.RelativeColumn(1f);   // Count
                        cd.RelativeColumn(1.4f); // Amount
                    });

                    void HCell(IContainer cell, string txt)
                    {
                        cell.Background(Colors.Indigo.Lighten4).Padding(3).AlignCenter().Text(txt)
                            .FontSize(7.5f).Bold().FontColor(Colors.Indigo.Darken4);
                    }

                    t.Header(h =>
                    {
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Cashier" : "اسم الكاشير"));
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Count" : "الطلبات"));
                        h.Cell().Element(cell => HCell(cell, _isEnglish ? "Amount" : "المبلغ"));
                    });

                    var cashiers = _orders
                        .GroupBy(o => !string.IsNullOrWhiteSpace(o.CashierName) ? o.CashierName : (!string.IsNullOrWhiteSpace(o.StaffName) ? o.StaffName : "غير محدد"))
                        .Select(g => new
                        {
                            Cashier = g.Key,
                            Count = g.Count(),
                            Amount = g.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.GrandTotal ?? 0))
                        })
                        .OrderByDescending(x => x.Amount)
                        .Take(5)
                        .ToList();

                    int cIdx = 0;
                    foreach (var cs in cashiers)
                    {
                        var bg = cIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                        void DCell(IContainer cell, string txt, bool bold = false)
                        {
                            var tVal = cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                                .Padding(2.5f).AlignCenter().Text(txt).FontSize(7);
                            if (bold) tVal.Bold();
                        }
                        DCell(t.Cell(), cs.Cashier, bold: true);
                        DCell(t.Cell(), cs.Count.ToString());
                        DCell(t.Cell(), $"{cs.Amount:N1}", bold: true);
                        cIdx++;
                    }
                });
            });
        });
    }

    private void ComposeA4OrdersSummaryTable(ColumnDescriptor col)
    {
        col.Item().PaddingTop(2).Column(c =>
        {
            c.Item().Text(_isEnglish ? "Voided Orders Summary Log" : "سجل ملخص الطلبات الملغاة")
                .FontSize(9.5f).Bold().FontColor(Colors.Grey.Darken3);

            c.Item().PaddingTop(3).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(28);   // Order #
                    cd.ConstantColumn(52);   // Type
                    cd.ConstantColumn(75);   // Time
                    cd.RelativeColumn(1.3f); // Cashier
                    cd.RelativeColumn(1.2f); // Voided By
                    cd.RelativeColumn(1.8f); // Reason
                    cd.ConstantColumn(55);   // Status
                    cd.RelativeColumn(1f);   // Before
                    cd.RelativeColumn(1f);   // Voided
                    cd.RelativeColumn(1f);   // After
                });

                void HCell(IContainer cell, string txt)
                {
                    cell.Background(Colors.Grey.Darken3).Padding(3).AlignCenter().Text(txt)
                        .FontSize(7).Bold().FontColor(Colors.White);
                }

                t.Header(h =>
                {
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Order#" : "رقم الطلب"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Type" : "النوع"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Date/Time" : "التاريخ والوقت"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Cashier" : "الكاشير"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Voided By" : "حذف بواسطة"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Reason" : "سبب الإلغاء"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Void Type" : "نوع الإلغاء"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Before" : "قبل الحذف"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "Voided" : "المحذوف"));
                    h.Cell().Element(cell => HCell(cell, _isEnglish ? "After" : "بعد الحذف"));
                });

                int rowIdx = 0;
                foreach (var o in _orders)
                {
                    bool isFullVoid = o.OrderState == "Voided";
                    decimal voidedAmt = (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (isFullVoid ? (o.GrandTotal ?? 0) : 0);
                    decimal beforeAmt = isFullVoid ? (voidedAmt > 0 ? voidedAmt : (o.GrandTotal ?? o.SubTotal ?? 0)) : ((o.GrandTotal ?? 0) + voidedAmt);
                    decimal afterAmt = isFullVoid ? 0 : (o.GrandTotal ?? 0);

                    var bg = rowIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                    void DCell(IContainer cell, string txt, bool bold = false, Color? color = null)
                    {
                        var tVal = cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(2.5f).AlignCenter().Text(txt).FontSize(7);
                        if (bold) tVal.Bold();
                        if (color.HasValue) tVal.FontColor(color.Value);
                    }

                    DCell(t.Cell(), $"#{o.OrderId}", bold: true);
                    DCell(t.Cell(), o.OrderType ?? "-");
                    DCell(t.Cell(), o.OrderDate.HasValue ? o.OrderDate.Value.ToString("dd/MM HH:mm") : "-");
                    DCell(t.Cell(), o.CashierName ?? o.StaffName ?? "-");
                    DCell(t.Cell(), !string.IsNullOrWhiteSpace(o.VoidByName) ? o.VoidByName : "-");
                    DCell(t.Cell(), !string.IsNullOrWhiteSpace(o.VoidReason) ? o.VoidReason : "-");
                    DCell(t.Cell(), isFullVoid ? "إلغاء كلي" : "إلغاء جزئي", bold: true, color: isFullVoid ? Colors.Red.Darken3 : Colors.Purple.Darken3);
                    DCell(t.Cell(), $"{beforeAmt:N1}");
                    DCell(t.Cell(), $"{voidedAmt:N1}", bold: true, color: Colors.Red.Darken3);
                    DCell(t.Cell(), $"{afterAmt:N1}");

                    rowIdx++;
                }

                // Total Row
                void TCell(IContainer cell, string txt)
                {
                    cell.Background(Colors.Grey.Lighten3).BorderTop(1.5f).BorderColor(Colors.Grey.Darken2)
                        .Padding(3).AlignCenter().Text(txt).FontSize(7.5f).Bold().FontColor(Colors.Black);
                }

                decimal grandVoid = _orders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? (o.GrandTotal ?? 0) : 0));

                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, _isEnglish ? "Total" : "الإجمالي"));
                t.Cell().Element(cell => TCell(cell, $"{_orders.Count} طلبات"));
                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, ""));
                t.Cell().Element(cell => TCell(cell, $"{grandVoid:N2} {Currency}"));
                t.Cell().Element(cell => TCell(cell, ""));
            });
        });
    }

    private void ComposeThermalHeader(ColumnDescriptor col)
    {
        col.Item().AlignCenter().Text(_branchName).FontSize(10).Bold();
        col.Item().AlignCenter().Text(_isEnglish ? "Void Summary Report" : "تقرير ملخص عمليات الحذف").FontSize(9).Bold();
        col.Item().AlignCenter().Text($"{_fromDate:dd/MM/yyyy} - {_toDate:dd/MM/yyyy}").FontSize(7.5f);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    private void ComposeThermalContent(ColumnDescriptor col)
    {
        int totalOrders = _orders.Count;
        int fullVoids = _orders.Count(o => o.OrderState == "Voided");
        int partialVoids = totalOrders - fullVoids;
        decimal totalVoidAmount = _orders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? (o.GrandTotal ?? 0) : 0));

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Void Amount:" : "إجمالي المبالغ الملغاة:").Bold().FontSize(8);
            r.RelativeItem().AlignRight().Text($"{totalVoidAmount:N2} {Currency}").Bold().FontSize(8);
        });
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Total Orders:" : "إجمالي الطلبات:").FontSize(7.5f);
            r.RelativeItem().AlignRight().Text($"{totalOrders}").FontSize(7.5f);
        });
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(_isEnglish ? "Full / Partial:" : "كلي / جزئي:").FontSize(7.5f);
            r.RelativeItem().AlignRight().Text($"{fullVoids} / {partialVoids}").FontSize(7.5f);
        });

        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

        foreach (var o in _orders.Take(50))
        {
            bool isFull = o.OrderState == "Voided";
            decimal vAmt = (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.GrandTotal ?? 0);
            col.Item().PaddingVertical(1).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem(2).Text($"#{o.OrderId} - {o.OrderType} ({(isFull ? "كلي" : "جزئي")})").Bold().FontSize(7.5f);
                    r.RelativeItem(1).AlignRight().Text($"{vAmt:N1}").Bold().FontSize(7.5f);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text($"بواسطة: {o.VoidByName ?? o.CashierName ?? "-"}").FontSize(7);
                    r.RelativeItem().AlignRight().Text($"السبب: {o.VoidReason ?? "-"}").FontSize(7);
                });
            });
        }
    }

    private void ComposeThermalFooter(ColumnDescriptor col)
    {
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        col.Item().AlignCenter().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
    }
}
