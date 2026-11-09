using POS.Contract.Models.ReceiptModels;

namespace POS.Reports.ReportsMakerServices;

public class ReceiptDocument : IDocument
{
    private readonly List<TableItem> _items;
    private readonly Receipt receipt;

    public ReceiptDocument(Receipt _receipt, List<TableItem> items)
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
                    BuildPaymentMethod(ref column);
                    //BuildBarcodeOrNoteSection(ref column);
                });

            page.Footer()
                .Column(column => { BuildFooter(ref column); });
        });
    }

    private void ConfigurePage(ref PageDescriptor page)
    {
        page.ContinuousSize(7.2f, Unit.Centimetre); // 72mm printable width
        page.PageColor(Colors.White);
        page.MarginTop(5);
        page.MarginRight(2);
        page.MarginBottom(5);
        page.MarginLeft(2);
        page.DefaultTextStyle(
            TextStyle.Default
                .FontFamily("Noto Sans", "Noto Sans Arabic")
                .FontSize(12)
                .Bold()); // Bold for better darkness on thermal printers
    }

    private void AddHeader(ref ColumnDescriptor column)
    {
        // Store Name at the top
        if (!string.IsNullOrWhiteSpace(receipt.StoreName) && !receipt.StoreName.Equals("Store", StringComparison.OrdinalIgnoreCase) && !receipt.StoreName.Equals("Store Name", StringComparison.OrdinalIgnoreCase))
        {
            column.Item()
                .Text(text =>
                {
                    text.Span(receipt.StoreName)
                        .Bold()
                        .FontSize(18);
                    text.AlignCenter();
                });
        }

        // 🖼️ Logo section underneath Store Name
        if (!string.IsNullOrEmpty(receipt.LogoPath) && File.Exists(receipt.LogoPath))
        {
            column.Item()
            .PaddingTop(5)
            .AlignCenter()
            .MaxHeight(100)
            .MaxWidth(180)
            .Image(receipt.LogoPath)
            .FitArea();
        }

        column.Item()
            .PaddingTop(8)
            .Text(text =>
            {
                text.Span(receipt.Id.ToString("0.##"))
                    .Bold()
                    .FontSize(18);
                
                if (receipt.IsFollowUp)
                {
                    text.Span(" - تابع")
                        .Bold()
                        .FontSize(18)
                        .FontColor(Colors.Grey.Medium);
                }
                
                text.AlignCenter();
            });

        if (receipt.IsHospitality)
        {
            column.Item().PaddingTop(2).Text(text => {
                text.Span("ضـيـافـة")
                    .Bold()
                    .FontSize(22)
                    .FontColor(Colors.Red.Medium);
                text.AlignCenter();
            });

            if (!string.IsNullOrEmpty(receipt.HospitalityResponsibleName))
            {
                column.Item().PaddingRight(5).Text(text => {
                    text.Span(receipt.HospitalityResponsibleName)
                        .FontSize(14);
                    text.Span(" :المسؤول") // Reversed for RTL look in LTR container if needed, but better just AlignRight
                        .Bold()
                        .FontSize(14);
                    text.AlignRight();
                });
            }

            if (!string.IsNullOrEmpty(receipt.HospitalityReason))
            {
                column.Item().PaddingRight(5).Text(text => {
                    text.Span(receipt.HospitalityReason)
                        .FontSize(12);
                    text.Span(" :السبب")
                        .Bold()
                        .FontSize(12);
                    text.AlignRight();
                });
            }
        }

        if (receipt.IsStaffMeal)
        {
            column.Item().PaddingTop(2).Text(text => {
                text.Span("وجبة موظف")
                    .Bold()
                    .FontSize(20);
                text.AlignCenter();
            });

            if (!string.IsNullOrEmpty(receipt.StaffMealEmployeeName))
            {
                column.Item().Text(text => {
                    text.Span("الموظف: ")
                        .Bold()
                        .FontSize(14);
                    text.Span(receipt.StaffMealEmployeeName)
                        .FontSize(14);
                    text.AlignCenter();
                });
            }
        }

        /*Header * Type */
        column.Item()
            .PaddingTop(1)
            .Text(text =>
            {
                if (receipt.IsCopy && !receipt.IsVoid)
                {
                    text.Span("*********COPY**********")
                        .Bold()
                        .FontSize(20)
                        .FontColor(Colors.Red.Medium);
                    text.EmptyLine();
                }

                // Only print ReceiptType if it's NOT a special mode (Hospitality/Staff) 
                // to avoid double labeling since we print custom headers above
                if (!receipt.IsHospitality && !receipt.IsStaffMeal)
                {
                    text.Span(receipt.ReceiptType)
                        .Bold()
                        .FontSize(18);
                }

                text.AlignCenter();
            });

        if (receipt.IsVoid)
        {
            column.Item()
                .PaddingTop(6)
                .Border(3)
                .BorderColor(Colors.Red.Medium)
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("X  ملغي  X")
                        .Bold()
                        .FontSize(40)
                        .FontColor(Colors.Red.Medium);
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
                    .Text(ArabicConstStrings.From)
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
                i.Price,
                i.IsVoided,
                i.HasDiscount,
                i.DiscountPercentage,
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
                    Price = first.Price,
                    Quantity = displayQty,
                    TotalAmount = totalQty > 0 
                        ? g.Sum(x => x.TotalAmount ?? (x.Price * x.Quantity) ?? 0)
                        : (displayQty * (first.Price ?? 0)),
                    Attributes = first.Attributes?.Where(a => a.Id < 5000).ToList(),
                    IsVoided = receipt.IsVoid || (totalQty == 0 && (first.IsVoided || totalVoidQty > 0)),
                    VoidAmount = totalVoidQty,
                    HasDiscount = first.HasDiscount,
                    DiscountPercentage = first.DiscountPercentage,
                    LineComment = g.Key.LineComment
                };
            }).ToList();

        column.Item()
            .PaddingTop(3)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.5f);  // اجمالي
                    columns.RelativeColumn(2.5f);  // السعر
                    columns.RelativeColumn(3.5f);  // الصنف
                    columns.RelativeColumn(1.5f);  // كمية
                });

                // Table Header
                table.Header(header =>
                {
                    header.Cell()
                        .Element(CellStyle)
                        .Text(ArabicConstStrings.Total)
                        .AlignCenter()
                        .Bold();

                    header.Cell()
                        .Element(CellStyle)
                        .Text(ArabicConstStrings.Price)
                        .AlignCenter()
                        .Bold();

                    header.Cell()
                        .Element(CellStyle)
                        .Text(ArabicConstStrings.Name)
                        .AlignCenter()
                        .Bold();

                    header.Cell()
                        .Element(CellStyle)
                        .Text(ArabicConstStrings.Quantity)
                        .AlignCenter()
                        .Bold();
                });

                foreach (TableItem item in groupedItems)
                {
                    table.Cell().Element(CellStyle).Text(item.TotalAmount?.ToString("0.##")).AlignCenter();
                    table.Cell().Element(CellStyle).Text(item.Price?.ToString("0.##")).AlignCenter();
                    table.Cell().Element(CellStyle).AlignRight().Text(text => {
                        text.Span(item.NameAr ?? item.Name);
                        if (receipt.IsVoid || (item.IsVoided == true && item.Quantity == 0) || (item.Quantity > 0 && item.VoidAmount > 0 && item.TotalAmount == 0)) {
                            text.Span(" (ملغي)").FontColor(Colors.Red.Medium);
                        }
                    });
                    
                    var qtyText = item.Quantity.ToString("0.##");
                    if (item.ByWeight) qtyText += " كجم";
                    table.Cell().Element(CellStyle).Text(qtyText).AlignCenter();

                    // Add Voided hint if available and item still has active quantity
                    if (item.VoidAmount > 0 && item.Quantity > 0)
                    {
                        table.Cell().ColumnSpan(4)
                            .Element(CellStyle)
                            .PaddingRight(45)
                            .Text($"✖ ملغي: {item.VoidAmount.Value.ToString("0.##")}")
                            .FontSize(10)
                            .FontColor(Colors.Red.Medium)
                            .AlignRight();
                    }

                    // Add Discount if available
                    if (item.HasDiscount)
                    {
                        var discountText = item.DiscountPercentage > 0 
                            ? $"{item.DiscountPercentage}%" 
                            : item.TotalDiscountPrice?.ToString("0.##");

                        table.Cell().ColumnSpan(4)
                            .Element(CellStyle)
                            .PaddingRight(45)
                            .Text($"➤ {ArabicConstStrings.Discount}: {discountText} (-{item.TotalDiscountPrice?.ToString("0.##")})")
                            .FontSize(10)
                            .FontColor(Colors.Red.Medium)
                            .AlignEnd();
                    }

                    // Add attributes if available
                    if (item.Attributes?.Any(a => a.Id < 5000) == true)
                    {
                        var groupedAttrs = item.Attributes
                            .Where(a => a.Id < 5000)
                            .GroupBy(a => a.Name)
                            .Select(g => new { Name = g.Key, Count = item.ByWeight ? g.Count() : g.Count() * item.Quantity });

                        foreach (var attr in groupedAttrs)
                        {
                            table.Cell().ColumnSpan(4)
                                .Element(CellStyle)
                                .PaddingRight(45)
                                .Text($"{attr.Name} ⟵{(attr.Count > 1 && !item.ByWeight ? $" ({attr.Count:N0})" : "")}")
                                .FontSize(10)
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
                        table.Cell().ColumnSpan(4)
                            .Element(CellStyle)
                            .PaddingRight(25)
                            .Text($"{lineComment} ⟵")
                            .FontSize(10)
                            .Bold()
                            .AlignEnd();
                    }

                }

                if (receipt.SubTotal != 0 && receipt.SubTotal.HasValue)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(receipt.SubTotal.Value.ToString("0.##")).FontSize(12).Bold().AlignCenter();
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(ArabicConstStrings.SubTotal).FontSize(12).Bold().AlignCenter();
                }

                if (receipt.Services != 0 && receipt.Services.HasValue)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(receipt.Services.Value.ToString("0.##")).FontSize(12).Bold().AlignCenter();
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(ArabicConstStrings.Service).FontSize(12).Bold().AlignCenter();
                }

                if (receipt.Tax != 0 && receipt.Tax.HasValue)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(receipt.Tax.Value.ToString("0.##")).FontSize(12).Bold().AlignCenter();
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(ArabicConstStrings.Tax).FontSize(12).Bold().AlignCenter();
                }

                if (receipt.Discount != 0 && receipt.Discount.HasValue)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(receipt.Discount.Value.ToString("0.##")).FontSize(14).Bold().AlignCenter();
                    table.Cell().ColumnSpan(2).PaddingTop(8).Text(ArabicConstStrings.Discount).FontSize(12).Bold().AlignCenter();
                }

                table.Cell()
                    .ColumnSpan(2)
                    .PaddingTop(8)
                    .Text((receipt.TotalAmount ?? 0).ToString("0.##"))
                    .FontSize(20)
                    .Bold()
                    .AlignCenter();

                table.Cell()
                    .ColumnSpan(2)
                    .PaddingTop(8)
                    .Text(ArabicConstStrings.Total)
                    .FontSize(18)
                    .Bold()
                    .AlignCenter();
            });
    }

    private void BuildPaymentMethod(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(13)
            .PaddingBottom(-2)
            .Text(receipt.PaymentMethod)
            .AlignCenter()
            .Bold()
            .FontSize(15)
            .AlignCenter();
    }

    private void BuildFooter(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(8)
            .Text(receipt.FooterMessage)
            .Bold()
            .FontSize(10)
            .AlignCenter();

        column.Item()
            .AlignCenter()
            .PaddingTop(5)
            .Text(text =>
            {
                text.Line("New Tech Company for Software Development").FontSize(9);
                text.Line("Contact: 01033964899").FontSize(9);
                text.AlignCenter();
            });

    }

    private static IContainer CellStyle(IContainer container)
        => container.Border(1)
            .Padding(2);
}



