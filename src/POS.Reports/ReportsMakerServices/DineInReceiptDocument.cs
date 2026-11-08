using POS.Contract.Models.ReceiptModels;
using POS.Contract.Models.ReceiptModels.DineIn;

namespace POS.Reports.ReportsMakerServices;

public class DineInReceiptDocument : IDocument
{
    private readonly List<TableItem> _items;
    private readonly DineInReceipt receipt;

    public DineInReceiptDocument(DineInReceipt _receipt, List<TableItem> items)
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
                    BuildTotals(ref column);
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

        column.Item()
           .Text(text =>
           {
               text.Span(receipt.StoreName)
                   .Bold()
                   .FontSize(18);
               text.AlignCenter();
           });

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
                text.Span(receipt.Id.ToString())
                    .Bold()
                    .FontSize(18);
                
                if (receipt.IsFollowUp)
                {
                    text.Span(" - ????")
                        .Bold()
                        .FontSize(18)
                        .FontColor(Colors.Red.Medium);
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
                    text.Span(" :???????")
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
                    text.Span(" :?????")
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
                if (receipt.IsClosing)
                {
                    text.Span("END")
                        .Bold()
                        .FontSize(20)
                        .FontColor(Colors.Red.Medium);
                    text.EmptyLine();
                }
                else if (receipt.IsCopy)
                {
                    text.Span("*********COPY**********")
                        .Bold()
                        .FontSize(20)
                        .FontColor(Colors.Red.Medium);
                    text.EmptyLine();
                }

                if (!receipt.IsHospitality && !receipt.IsStaffMeal)
                {
                    text.Span(receipt.ReceiptType)
                        .Bold()
                        .FontSize(18);
                }

                if (!receipt.IsClosing && receipt.IsCopy)
                {
                    text.EmptyLine();
                    text.Span("*********COPY**********")
                        .Bold()
                        .FontSize(20)
                        .FontColor(Colors.Red.Medium);
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
                    text.Span("X  ????  X")
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

                table.Cell()
                    .Element(CellStyle)
                    .Text(receipt.CaptainName)
                    .AlignCenter();

                table.Cell()
                    .Element(CellStyle)
                    .Text("????? ??????")
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
                AttributesHash = string.Join("|", i.Attributes?.OrderBy(a => a.Id).Select(a => a.Id) ?? Enumerable.Empty<int?>())
            })
            .Select(g =>
            {
                var first = g.First();
                return new TableItem
                {
                    Id = first.Id,
                    Name = first.Name,
                    Price = first.Price,
                    Quantity = g.Sum(x => x.Quantity),
                    TotalAmount = g.Sum(x => x.TotalAmount ?? (x.Price * x.Quantity) ?? 0),
                    Attributes = first.Attributes,
                    IsVoided = first.IsVoided,
                    VoidAmount = g.Sum(x => x.VoidAmount ?? 0),
                    HasDiscount = first.HasDiscount,
                    DiscountPercentage = first.DiscountPercentage,
                    TotalDiscountPrice = g.Sum(x => x.TotalDiscountPrice ?? 0)
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
                        text.Span(item.Name);
                        if (item.IsVoided == true && item.Quantity == 0) {
                            text.Span(" (????)").FontColor(Colors.Red.Medium);
                        }
                    });
                    var qtyText = item.Quantity.ToString("0.##");
if (item.ByWeight) qtyText += " ???";
table.Cell().Element(CellStyle).Text(qtyText).AlignCenter();

                    // Add Voided hint if available and item still has active quantity
                    if (item.VoidAmount > 0 && item.Quantity > 0)
                    {
                        table.Cell().ColumnSpan(4)
                            .Element(CellStyle)
                            .PaddingRight(45)
                            .Text($"? ????: {item.VoidAmount.Value.ToString("0.##")}")
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
                            .Text($"? {ArabicConstStrings.Discount}: {discountText} (-{item.TotalDiscountPrice?.ToString("0.##")})")
                            .FontSize(10)
                            .FontColor(QuestPDF.Helpers.Colors.Red.Medium)
                            .AlignEnd();
                    }

                    // Add attributes if available
                    if (item.Attributes?.Any() == true)
                    {
                        var groupedAttrs = item.Attributes
                            .GroupBy(a => a.Name)
                            .Select(g => new { Name = g.Key, Count = item.ByWeight ? g.Count() : g.Count() * item.Quantity });

                        foreach (var attr in groupedAttrs)
                        {
                            table.Cell().ColumnSpan(4)
                                .Element(CellStyle)
                                .PaddingRight(45)
                                .Text($"{attr.Name} ?{(attr.Count > 1 && !item.ByWeight ? $" ({attr.Count:N0})" : "")}")
                                .FontSize(10)
                                .AlignEnd();
                        }
                    }

                }
            });
    }

    private void BuildTotals(ref ColumnDescriptor column)
    {
        column.Item().PaddingTop(12).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Text(receipt.TotalAmount?.ToString("0.##")).FontSize(13).Bold().AlignCenter();
            table.Cell().Text(ArabicConstStrings.SubTotal).Bold().FontSize(15).Bold().AlignCenter();

            if (!string.IsNullOrEmpty(receipt.ServiceAmount) && receipt.ServiceAmount != "0" && receipt.ServiceAmount != "0.00")
            {
                table.Cell().Text(receipt.ServiceAmount).FontSize(13).Bold().AlignCenter();
                table.Cell().Text(ArabicConstStrings.Service).Bold().FontSize(15).Bold().AlignCenter();
            }

            if (!string.IsNullOrEmpty(receipt.TaxAmount) && receipt.TaxAmount != "0" && receipt.TaxAmount != "0.00")
            {
                table.Cell().Text(receipt.TaxAmount).FontSize(13).Bold().AlignCenter();
                table.Cell().Text(ArabicConstStrings.Tax).Bold().FontSize(15).Bold().AlignCenter();
            }

            if (receipt.Discount.HasValue && Math.Abs(receipt.Discount.Value) > 0.01m)
            {
                table.Cell().Text(receipt.Discount.Value.ToString("0.##")).FontSize(13).Bold().AlignCenter();
                table.Cell().Text(ArabicConstStrings.Discount).Bold().FontSize(15).Bold().AlignCenter();
            }

            table.Cell().Text(receipt.TotalOrder).FontSize(13).Bold().AlignCenter();
            table.Cell().Text(ArabicConstStrings.Total).FontSize(15).Bold().AlignCenter();
        });
    }

    private void BuildPaymentMethod(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(8)
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
            .PaddingTop(5)
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

