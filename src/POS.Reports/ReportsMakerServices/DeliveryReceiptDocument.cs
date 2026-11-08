using POS.Contract.Models.ReceiptModels;
using POS.Contract.Models.ReceiptModels.Delivery;

namespace POS.Reports.ReportsMakerServices;

public class DeliveryReceiptDocument : IDocument
{
    private readonly DeliveryReceipt _receipt;
    private readonly List<TableItem> _items;

    public DeliveryReceiptDocument(DeliveryReceipt receipt, List<TableItem> items)
    {
        _receipt = receipt;
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
                    BuildOrderInfo(ref column);
                    BuildItemsTable(ref column);
                    BuildTotals(ref column);
                    BuildPaymentMethod(ref column);
                });

            page.Footer()
                .Column(column => BuildFooter(ref column));
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
            .AlignCenter()
            .Text(_receipt.StoreName)
            .Bold()
            .FontSize(18);

        if (!string.IsNullOrEmpty(_receipt.LogoPath) && File.Exists(_receipt.LogoPath))
        {
            column.Item()
            .PaddingTop(5)
            .AlignCenter()
            .MaxHeight(100)
            .MaxWidth(180)
            .Image(_receipt.LogoPath)
            .FitArea();
        }

        column.Item()
            .PaddingTop(5)
            .AlignCenter()
            .Text(text =>
            {
                if (_receipt.IsCopy)
                {
                    text.Span("*********COPY**********")
                        .Bold()
                        .FontSize(11)
                        .FontColor(Colors.Red.Medium);
                    text.EmptyLine();
                }

                if (!_receipt.IsHospitality && !_receipt.IsStaffMeal)
                {
                    text.Span(_receipt.ReceiptType ?? "توصيل طلبات")
                        .Bold()
                        .FontSize(12);
                }

            });

        column.Item()
            .AlignCenter()
            .PaddingTop(2)
            .Text(text =>
            {
                text.Span(_receipt.Id.ToString("0.##")).Bold().FontSize(20);
                text.AlignCenter();
            });

        // Follow-up badge � separate row to avoid BiDi scramble
        if (_receipt.IsFollowUp)
        {
            column.Item()
                .AlignCenter()
                .PaddingTop(1)
                .Text(text =>
                {
                    text.Span("◄ تـابـع ►")
                        .Bold()
                        .FontSize(16)
                        .FontColor(Colors.Red.Medium);
                    text.AlignCenter();
                });
        }

        // Remote branch info � separate row to avoid BiDi scramble
        if (_receipt.RemoteOrderId.HasValue && _receipt.RemoteOrderId != 0)
        {
            column.Item()
                .AlignCenter()
                .PaddingTop(1)
                .Text(text =>
                {
                    text.Span("فرع: " + _receipt.StoreName + "  |  رقم: " + _receipt.RemoteOrderId)
                        .Bold()
                        .FontSize(12)
                        .FontColor(Colors.Blue.Medium);
                    text.AlignCenter();
                });
        }

        if (_receipt.IsHospitality)
        {
            column.Item().PaddingTop(2).Text(text => {
                text.Span("ضـيـافـة")
                    .Bold()
                    .FontSize(18)
                    .FontColor(Colors.Red.Medium);
                text.AlignCenter();
            });

            if (!string.IsNullOrEmpty(_receipt.HospitalityResponsibleName))
            {
                column.Item().PaddingRight(5).Text(text => {
                    text.Span(_receipt.HospitalityResponsibleName)
                        .FontSize(11);
                    text.Span(" :المسؤول")
                        .Bold()
                        .FontSize(11);
                    text.AlignRight();
                });
            }

            if (!string.IsNullOrEmpty(_receipt.HospitalityReason))
            {
                column.Item().PaddingRight(5).Text(text => {
                    text.Span(_receipt.HospitalityReason)
                        .FontSize(10);
                    text.Span(" :السبب")
                        .Bold()
                        .FontSize(10);
                    text.AlignRight();
                });
            }
        }

        if (_receipt.IsStaffMeal)
        {
            column.Item().PaddingTop(2).Text(text => {
                text.Span("وجبة موظف")
                    .Bold()
                    .FontSize(20);
                text.AlignCenter();
            });

            if (!string.IsNullOrEmpty(_receipt.StaffMealEmployeeName))
            {
                column.Item().Text(text => {
                    text.Span("الموظف: ")
                        .Bold()
                        .FontSize(14);
                    text.Span(_receipt.StaffMealEmployeeName)
                        .FontSize(14);
                    text.AlignCenter();
                });
            }
        }

        if (_receipt.ParentOrderId.HasValue)
        {
            column.Item()
                .AlignCenter()
                .Text($"تكملة لرقم {_receipt.ParentOrderId}")
                .Bold()
                .FontSize(11)
                .FontColor(Colors.Grey.Medium);
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().PaddingTop(5).Text(_receipt.DateCreated.ToString("d/M/yyyy")).AlignCenter();
            table.Cell().PaddingTop(5).Text(_receipt.DateCreated.ToString("h:mm:ss tt")).AlignCenter();
        });

        if (_receipt.IsVoid)
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

    private void BuildOrderInfo(ref ColumnDescriptor column)
    {
        column.Item().PaddingTop(5).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(5.5f); // Value column
                columns.RelativeColumn(2.5f); // Label column
            });

            table.Cell().Element(CellStyle).Text(_receipt.CashierName).AlignRight();
            table.Cell().Element(CellStyle).Text("الكاشير").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(FormatPhoneNumbers()).AlignRight();
            table.Cell().Element(CellStyle).Text("تليفون").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(_receipt.CustomerName).AlignRight();
            table.Cell().Element(CellStyle).Text("العميل").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(_receipt.CustomerAddress).AlignRight();
            table.Cell().Element(CellStyle).Text("العنوان").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(_receipt.Building).AlignRight();
            table.Cell().Element(CellStyle).Text("رقم المبنى").Bold().AlignRight();

            // Nested table for Floor and Flat within a single row span
            table.Cell().ColumnSpan(2).Padding(0).Table(innerTable =>
            {
                innerTable.ColumnsDefinition(innerCols =>
                {
                    innerCols.RelativeColumn(2f); // Floor Value
                    innerCols.RelativeColumn(2f); // Floor Label
                    innerCols.RelativeColumn(2f); // Flat Value
                    innerCols.RelativeColumn(2f); // Flat Label
                });

                innerTable.Cell().Element(CellStyle).Text(_receipt.FloorNumber).AlignCenter();
                innerTable.Cell().Element(CellStyle).Text("رقم الدور").Bold().AlignCenter();
                innerTable.Cell().Element(CellStyle).Text(_receipt.FlatNumber).AlignCenter();
                innerTable.Cell().Element(CellStyle).Text("شقة رقم").Bold().AlignCenter();
            });

            table.Cell().Element(CellStyle).Text(_receipt.ZoneName).AlignRight();
            table.Cell().Element(CellStyle).Text("المنطقة").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(_receipt.AddressNote ?? "-").AlignRight();
            table.Cell().Element(CellStyle).Text("ملاحظات").Bold().AlignRight();

            table.Cell().Element(CellStyle).Text(_receipt.DeliveryDisplayName ?? _receipt.DeliveryName).AlignRight();
            table.Cell().Element(CellStyle).Text("الطيار").Bold().AlignRight();
        });
    }

    private string FormatPhoneNumbers()
    {
        if (string.IsNullOrEmpty(_receipt.CustomerSecondPhone))
            return _receipt!.CustomerFirstPhone!;

        return $"{_receipt.CustomerFirstPhone} - {_receipt.CustomerSecondPhone}";
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
                    IsVoided = _receipt.IsVoid || (totalQty == 0 && (first.IsVoided || totalVoidQty > 0)),
                    VoidAmount = totalVoidQty,
                    HasDiscount = first.HasDiscount,
                    DiscountPercentage = first.DiscountPercentage,
                    ByWeight = first.ByWeight,
                    LineComment = g.Key.LineComment
                };
            }).ToList();

        column.Item()
            .PaddingTop(6)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.5f); // اجمالي 
                    columns.RelativeColumn(2.5f); // السعر
                    columns.RelativeColumn(3.5f); // الصنف 
                    columns.RelativeColumn(1.5f); // كمية
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
                        if (_receipt.IsVoid || (item.IsVoided == true && item.Quantity == 0) || (item.Quantity > 0 && item.VoidAmount > 0 && item.TotalAmount == 0)) {
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

                    // Add line comment if available
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
            });
    }
    private void BuildTotals(ref ColumnDescriptor column)
    {
        column.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Text(_receipt.TotalAmount?.ToString("0.##")).FontSize(13).Bold().AlignCenter();
            table.Cell().Text("حساب الاصناف").Bold().FontSize(15).Bold().AlignCenter();

            table.Cell().Text(_receipt.DeliveryFees?.ToString("0.##")).FontSize(13).Bold().AlignCenter();
            table.Cell().Text("خدمة توصيل").Bold().FontSize(15).Bold().AlignCenter();

            table.Cell().Text(_receipt.TotalOrder?.ToString("0.##")).FontSize(13).Bold().AlignCenter();
            table.Cell().Text("الاجمالي").FontSize(13).Bold().AlignCenter();
        });
    }
    private void BuildPaymentMethod(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(8)
            .PaddingBottom(-2)
            .Text(_receipt.PaymentMethod)
            .AlignCenter()
            .Bold()
            .FontSize(15)
            .AlignCenter();
    }
    private void BuildFooter(ref ColumnDescriptor column)
    {
        column.Item()
            .PaddingTop(7)
            .Text(_receipt.FooterMessage)
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

