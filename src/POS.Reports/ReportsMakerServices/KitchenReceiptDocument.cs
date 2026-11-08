using POS.Contract.Models.ReceiptModels;
using POS.Contract.Models.ReceiptModels.Kitchen;

namespace POS.Reports.ReportsMakerServices;

public class KitchenReceiptDocument : IDocument
{
    private readonly List<TableItem> _items;
    private readonly KitchenReceipt receipt;

    public KitchenReceiptDocument(KitchenReceipt _receipt, List<TableItem> items)
    {
        receipt = _receipt;
        _items = items;
    }
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ConfigurePage(ref page);
            page.Content()
                .Column(column =>
                {
                    AddHeader(ref column);
                    BuildDateAndCashierInfo(ref column);
                    BuildItemsTable(ref column);
                    BuildKitchenNotes(ref column);
                });
        });
    }

    private void ConfigurePage(ref PageDescriptor page)
    {
        page.ContinuousSize(72, Unit.Millimetre); // Match printer internal width (72mm)
        page.PageColor(Colors.White);
        page.Margin(2); // Minimal margins
        page.DefaultTextStyle(
            TextStyle.Default
                .FontFamily("Noto Sans", "Noto Sans Arabic")
                .FontSize(12)
                .Bold()); // Bold for better darkness on thermal printers
    }

    private void AddHeader(ref ColumnDescriptor column)
    {
        column.Item()
           .Text(text =>
           {
               text.Span(receipt.KitchenType.ToString())
                   .Bold()
                   .FontSize(18);
               text.AlignCenter();
           });

        column.Item()
            .PaddingTop(6)
            .Text(text =>
            {
                text.Span(receipt.Id.ToString("0.##"))
                    .Bold()
                    .FontSize(18);
                
                if (receipt.IsFollowUp)
                {
                    text.Span(receipt.IsArabic ? " - تابع" : " - Follow Up")
                        .Bold()
                        .FontSize(18)
                        .FontColor(Colors.Red.Medium);
                }
                
                text.AlignCenter();
            });

        column.Item()
            .PaddingTop(6)
            .Text(text =>
            {
                text.Span(receipt.OrderType)
                    .Bold()
                    .FontSize(18);
                text.AlignCenter();
            });

        if (receipt.IsCopy)
        {
            column.Item()
                .PaddingTop(6)
                .AlignCenter()
                .Text("*********COPY**********")
                .Bold()
                .FontSize(18)
                .FontColor(Colors.Red.Medium);
        }

        if (receipt.IsVoid)
        {
            column.Item()
                .PaddingTop(6)
                .Border(3)
                .BorderColor(Colors.Red.Medium)
                .AlignCenter()
                .Text(text =>
                {
                    text.Span(receipt.IsArabic ? "X  ملغي  X" : "X  VOID  X")
                        .Bold()
                        .FontSize(40)
                        .FontColor(Colors.Red.Medium);
                });
        }

        if (receipt.ParentOrderId.HasValue)
        {
            column.Item()
                .AlignCenter()
                .Text(receipt.IsArabic ? $"تكملة لرقم {receipt.ParentOrderId}" : $"Continuation of #{receipt.ParentOrderId}")
                .Bold()
                .FontSize(11);
        }

        if (!string.IsNullOrEmpty(receipt.TableName))
        {
            column.Item()
                .PaddingTop(6)
                .Text(text =>
                {
                    text.Span($"{receipt.TableName} : {(receipt.IsArabic ? ArabicConstStrings.Table : "Table")}")
                        .Bold()
                        .FontSize(20);
                    text.AlignCenter();
                });
        }
    }

    private void BuildDateAndCashierInfo(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(5)
            .Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });

                table.Cell()
                    .Element(CellStyle)
                    .Text(receipt.DateCreated.ToString("d/MM/yyyy"))
                    .AlignCenter();

                table.Cell()
                    .Element(CellStyle)
                    .Text(receipt.DateCreated.ToString("hh:mm:ss tt"))
                    .AlignCenter();

                table.Cell()
                    .Element(CellStyle)
                    .Text(receipt.CashierName)
                    .AlignCenter();

                table.Cell()
                    .Element(CellStyle)
                    .Text(receipt.IsArabic ? ArabicConstStrings.From : "From")
                    .AlignCenter();
            });
    }

    private void BuildItemsTable(ref ColumnDescriptor column)
    {
        // Group items before printing
        var groupedItems = _items
            .GroupBy(i => new
            {
                i.Id,
                i.Name,
                i.IsVoided,
                LineComment = !string.IsNullOrWhiteSpace(i.LineComment)
                    ? i.LineComment
                    : (i.Attributes?.Any(a => a.Id >= 5000) == true
                        ? string.Join(", ", i.Attributes.Where(a => a.Id >= 5000).Select(a => a.Name))
                        : null),
                AttributesHash = string.Join("|", i.Attributes?.Where(a => a.Id < 5000).OrderBy(a => a.Id).Select(a => a.Id) ?? Enumerable.Empty<int?>())
            })
            .Select(g =>
            {
                var first = g.First();
                decimal totalQty = g.Sum(x => x.Quantity);
                decimal totalVoidQty = g.Sum(x => x.VoidAmount ?? 0);
                decimal displayQty = totalQty > 0 ? totalQty : (totalVoidQty > 0 ? totalVoidQty : totalQty);
                return new TableItem
                {
                    Id = first.Id,
                    Name = first.Name,
                    Quantity = displayQty,
                    VoidAmount = totalVoidQty,
                    Attributes = first.Attributes?.Where(a => a.Id < 5000).ToList(),
                    IsVoided = first.IsVoided || receipt.IsVoid,
                    ByWeight = first.ByWeight,
                    LineComment = g.Key.LineComment
                };
            }).ToList();

        bool hasWeight = groupedItems.Any(i => i.ByWeight);
        bool hasLongWeight = groupedItems.Any(i =>
            i.ByWeight && (FormatWeightFraction(i.Quantity, receipt.IsArabic)?.Length ?? 0) > 4);

        float nameColRatio = !hasWeight ? 8.5f : (hasLongWeight ? 6.2f : 7.2f);
        float qtyColRatio = !hasWeight ? 1.5f : (hasLongWeight ? 3.8f : 2.8f);

        column.Item()
            .PaddingTop(3)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(nameColRatio);
                    columns.RelativeColumn(qtyColRatio);
                });

                table.Header(header =>
                {
                    header.Cell()
                        .Element(CellStyle)
                        .Text(receipt.IsArabic ? ArabicConstStrings.Name : "Item")
                        .AlignCenter()
                        .Bold();

                    header.Cell()
                        .Element(CellStyle)
                        .Text(receipt.IsArabic ? ArabicConstStrings.Quantity : "Qty")
                        .AlignCenter()
                        .Bold();
                });

                foreach (TableItem item in groupedItems)
                {
                    table.Cell()
                        .Element(CellStyle)
                        .AlignMiddle()
                        .AlignRight()
                        .PaddingVertical(1)
                        .Text(text =>
                        {
                            text.Span(item.Name).Bold().FontSize(12);
                            // Only show (ملغي) if the entire receipt is a void receipt OR the item record is explicitly fully voided
                            if (receipt.IsVoid || (item.IsVoided == true && item.Quantity == 0))
                            {
                                text.Span(receipt.IsArabic ? " (ملغي)" : " (VOID)").FontColor(Colors.Red.Medium).Bold();
                            }
                        });

                    table.Cell()
                        .Element(CellStyle)
                        .AlignMiddle()
                        .AlignCenter()
                        .PaddingVertical(1)
                        .Text(text =>
                        {
                            if (item.ByWeight)
                            {
                                var fraction = FormatWeightFraction(item.Quantity, receipt.IsArabic);
                                if (!string.IsNullOrEmpty(fraction))
                                {
                                    text.Span(fraction).Bold().FontSize(10.5f);
                                    text.Span($" ({item.Quantity:0.##})").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                                }
                                else
                                {
                                    text.Span($"{item.Quantity:0.##} {(receipt.IsArabic ? "كجم" : "kg")}").Bold().FontSize(10.5f);
                                }
                            }
                            else
                            {
                                text.Span(item.Quantity.ToString("0.##")).Bold().FontSize(12);
                            }
                        });

                    if (item.Attributes?.Any(a => a.Id < 5000) == true)
                    {
                        var groupedAttrs = item.Attributes
                            .Where(a => a.Id < 5000)
                            .GroupBy(a => a.Name)
                            .Select(g => new { Name = g.Key, Count = item.ByWeight ? g.Count() : g.Count() * item.Quantity });

                        foreach (var attr in groupedAttrs)
                        {
                            table.Cell().ColumnSpan(2)
                                .Element(CellStyle)
                                .PaddingRight(25)
                                .Text($"{attr.Name} ⟵{(attr.Count > 1 && !item.ByWeight ? $" ({attr.Count:N0})" : "")}")
                                .FontSize(11)
                                .Bold()
                                .AlignEnd();
                        }
                    }

                    // Print per-item comment (LineComment or manual comments in Attributes)
                    var lineComment = !string.IsNullOrWhiteSpace(item.LineComment)
                        ? item.LineComment
                        : (item.Attributes?.Any(a => a.Id >= 5000) == true
                            ? string.Join(", ", item.Attributes.Where(a => a.Id >= 5000).Select(a => a.Name))
                            : null);

                    if (!string.IsNullOrWhiteSpace(lineComment))
                    {
                        table.Cell().ColumnSpan(2)
                            .Element(CellStyle)
                            .PaddingRight(20)
                            .Text($"{lineComment} ⟵")
                            .FontSize(11)
                            .Bold()
                            .AlignEnd();
                    }
                }
            });
    }

    private static string? FormatWeightFraction(decimal qty, bool isArabic)
    {
        if (qty <= 0) return null;

        int whole = (int)Math.Floor(qty);
        decimal frac = qty - whole;

        bool IsNear(decimal val, decimal target, decimal tolerance = 0.025m)
            => Math.Abs(val - target) <= tolerance;

        if (whole == 0)
        {
            if (IsNear(frac, 0.125m)) return isArabic ? "ثمن" : "1/8";
            if (IsNear(frac, 0.250m)) return isArabic ? "ربع" : "1/4";
            if (IsNear(frac, 0.375m)) return isArabic ? "ثمن وربع" : "3/8";
            if (IsNear(frac, 0.500m)) return isArabic ? "نصف" : "1/2";
            if (IsNear(frac, 0.625m)) return isArabic ? "نصف وثمن" : "5/8";
            if (IsNear(frac, 0.750m)) return isArabic ? "كيلو إلا ربع" : "3/4";
            if (IsNear(frac, 0.875m)) return isArabic ? "كيلو إلا ثمن" : "7/8";
            return null;
        }

        string wholeNameAr = whole switch
        {
            1 => "كيلو",
            2 => "2 كيلو",
            _ => $"{whole} كيلو"
        };

        if (frac < 0.025m)
            return isArabic ? $"{whole} كيلو" : $"{whole} kg";

        if (IsNear(frac, 0.125m))
            return isArabic ? $"{wholeNameAr} وثمن" : $"{whole} 1/8";

        if (IsNear(frac, 0.250m))
            return isArabic ? $"{wholeNameAr} وربع" : $"{whole} 1/4";

        if (IsNear(frac, 0.375m))
            return isArabic ? $"{wholeNameAr} وثمن وربع" : $"{whole} 3/8";

        if (IsNear(frac, 0.500m))
            return isArabic ? $"{wholeNameAr} ونصف" : $"{whole} 1/2";

        if (IsNear(frac, 0.625m))
            return isArabic ? $"{wholeNameAr} ونصف وثمن" : $"{whole} 5/8";

        if (IsNear(frac, 0.750m))
            return isArabic ? (whole == 1 ? "2 كيلو إلا ربع" : $"{whole + 1} كيلو إلا ربع") : $"{whole} 3/4";

        if (IsNear(frac, 0.875m))
            return isArabic ? $"{whole + 1} كيلو إلا ثمن" : $"{whole} 7/8";

        return null;
    }

    private void BuildKitchenNotes(ref ColumnDescriptor column)
    {
        if (!string.IsNullOrEmpty(receipt.KitchenNote))
        {
            column.Item()
                .PaddingTop(10)
                .AlignCenter()
                .Text(text =>
                {
                    text.Span(receipt.IsArabic ? "ملاحظات المطبخ" : "Kitchen Notes").FontSize(13).Bold();
                });

            column.Item()
                .PaddingTop(4)
                .AlignCenter()
                .Text(text =>
                {
                    var lines = receipt.KitchenNote.Split('\n');
                    foreach (var line in lines)
                    {
                        text.Span(line);
                        if (line != lines.Last())
                            text.EmptyLine()
                            .FontSize(10);
                    }
                });
        }
    }

    private static IContainer CellStyle(IContainer container)
        => container.Border(1)
            .Padding(2);
}



