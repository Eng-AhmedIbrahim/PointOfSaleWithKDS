using POS.Contract.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using POS.Contract.Dtos.OrderDtos;
using POS.Contract.Dtos.ReportingDtos;

namespace POS.Reports.ReportsMakerServices;

public static class ExcelReportExportService
{
    private static readonly XLColor PrimaryHeaderBg = XLColor.FromHtml("#1E3A5F");
    private static readonly XLColor SecondaryHeaderBg = XLColor.FromHtml("#2A5298");
    private static readonly XLColor SectionHeaderBg = XLColor.FromHtml("#334155");
    private static readonly XLColor TotalRowBg = XLColor.FromHtml("#F1F5F9");
    private static readonly XLColor TableBorderColor = XLColor.FromHtml("#CBD5E1");

    public static string SanitizeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Sheet1";
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        foreach (var c in invalid) name = name.Replace(c.ToString(), "");
        name = name.Trim();
        if (name.Length > 31) name = name.Substring(0, 31).Trim();
        return string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
    }

    private static void TryAddLogo(IXLWorksheet ws, int col)
    {
        try
        {
            string[] possiblePaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "ReceiptLogo", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "ReceiptLogo", "Logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "ReceiptLogo", "logo.png")
            };

            foreach (var p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    var pic = ws.AddPicture(p);
                    pic.MoveTo(ws.Cell(1, col));
                    pic.Scale(0.35);
                    break;
                }
            }
        }
        catch { }
    }

    private static void StyleSectionHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = SectionHeaderBg;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 10;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleTableHeader(IXLCell cell)
    {
        cell.Style.Fill.BackgroundColor = PrimaryHeaderBg;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 9.5;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = TableBorderColor;
    }

    private static void StyleDataRow(IXLRange range)
    {
        range.Style.Font.FontSize = 9;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = TableBorderColor;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = TableBorderColor;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleTotalRow(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = TotalRowBg;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 9.5;
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = PrimaryHeaderBg;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        range.Style.Border.BottomBorderColor = PrimaryHeaderBg;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void AddTwoColumnRow(IXLWorksheet ws, ref int row, string label, decimal value, XLColor? fontColor = null, bool isBold = false, bool isCurrency = true)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Font.FontSize = 9;
        if (isBold) ws.Cell(row, 1).Style.Font.Bold = true;

        var valCell = ws.Cell(row, 2);
        valCell.Value = value;
        valCell.Style.Font.FontSize = 9;
        if (isCurrency)
            valCell.Style.NumberFormat.Format = "#,##0.00";
        else
            valCell.Style.NumberFormat.Format = "#,##0";

        if (fontColor != null) valCell.Style.Font.FontColor = fontColor;
        if (isBold) valCell.Style.Font.Bold = true;

        ws.Range(row, 1, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 1, row, 2).Style.Border.BottomBorderColor = XLColor.FromHtml("#E2E8F0");
        row++;
    }

    private static void AddChannelRow(IXLWorksheet ws, ref int row, string channel, ModeSummaryDto data)
    {
        ws.Cell(row, 1).Value = channel;
        ws.Cell(row, 2).Value = data.OrderCount;
        ws.Cell(row, 3).Value = data.Subtotal; ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 4).Value = data.Discount; ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 5).Value = data.Service + data.DeliveryFees; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 6).Value = data.Tax; ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 7).Value = data.Total; ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 7).Style.Font.Bold = true;

        StyleDataRow(ws.Range(row, 1, row, 7));
        row++;
    }

    // 1. Sales Summary
    public static byte[] ExportSalesSummary(SalesSummaryDto summary, List<SalesItemSummaryDto> items, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "ملخص المبيعات" : "Sales Summary"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير ملخص المبيعات والتحليلات الشاملة" : "Sales Summary & Analytics Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 7);

        int row = 5;

        // 1. Financial Breakdown
        ws.Cell(row, 1).Value = isAr ? "المعاملات المالية والضريبة" : "Financial Breakdown";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        decimal grossSubtotal = summary.Overall.TotalSales + summary.Overall.TotalDiscount - summary.Overall.ServiceTotal - summary.Overall.TotalSalesTax;
        if (grossSubtotal <= 0) grossSubtotal = summary.Overall.TotalSales + summary.Overall.TotalDiscount;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي مبيعات الأصناف (قبل الخصم)" : "Items Subtotal (Before Disc.)", grossSubtotal);
        AddTwoColumnRow(ws, ref row, isAr ? "الخصومات الممنوحة" : "Discounts Given", -summary.Overall.TotalDiscount, XLColor.Red);
        AddTwoColumnRow(ws, ref row, isAr ? "خدمة الصالة" : "Dine-in Service", summary.DineIn.Service);
        AddTwoColumnRow(ws, ref row, isAr ? "خدمة التوصيل (الطيارين)" : "Delivery Fees", summary.Delivery.DeliveryFees > 0 ? summary.Delivery.DeliveryFees : summary.Delivery.Service);
        if (summary.Overall.TotalSalesTax > 0)
            AddTwoColumnRow(ws, ref row, isAr ? "ضريبة القيمة المضافة" : "VAT / Tax", summary.Overall.TotalSalesTax);
        AddTwoColumnRow(ws, ref row, isAr ? "صافي المبيعات المحققة" : "Net Recognized Sales", summary.Overall.TotalSales, XLColor.FromHtml("#166534"), isBold: true);

        row++;

        // 2. Drawer & Collections
        ws.Cell(row, 1).Value = isAr ? "حركة الدرج والتحصيلات" : "Drawer & Collections";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "التحصيل النقدي (كاش)" : "Cash Tendered", summary.Overall.CashAmount);
        AddTwoColumnRow(ws, ref row, isAr ? "التحصيل الإلكتروني (فيزا)" : "Credit Cards", summary.Overall.CreditAmount);
        if (summary.Overall.PendingAmount > 0)
            AddTwoColumnRow(ws, ref row, isAr ? "مبالغ معلقة / آجل عملاء" : "Pending / Uncollected", summary.Overall.PendingAmount, XLColor.Orange);
        if (summary.Overall.Expenses > 0)
            AddTwoColumnRow(ws, ref row, isAr ? "مصروفات الدرج" : "Drawer Expenses", -summary.Overall.Expenses, XLColor.Red);
        AddTwoColumnRow(ws, ref row, isAr ? "السيولة المتوقعة بالدرج" : "Expected Drawer Cash", summary.Overall.NetCash, XLColor.FromHtml("#166534"), isBold: true);

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي المحذوفات (الملغيات)" : "Total Voids", summary.Overall.VoidAmount, XLColor.Red, isBold: true);
        AddTwoColumnRow(ws, ref row, isAr ? "عدد طلبات الإلغاء" : "Void Count", summary.Overall.VoidCount, isCurrency: false);

        row += 2;

        // 3. Channels Table
        ws.Cell(row, 1).Value = isAr ? "المبيعات حسب نوع الطلب" : "Sales by Channel";
        ws.Range(row, 1, row, 7).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 7));
        row++;

        string[] channelHeaders = isAr 
            ? new[] { "النوع", "عدد الطلبات", "إجمالي الأصناف", "الخصم", "الخدمة والتوصيل", "الضريبة", "الصافي الإجمالي" }
            : new[] { "Channel", "Orders", "Subtotal", "Discount", "Service & Fees", "Tax", "Net Total" };

        for (int c = 0; c < channelHeaders.Length; c++)
        {
            ws.Cell(row, c + 1).Value = channelHeaders[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        AddChannelRow(ws, ref row, isAr ? "صالة" : "Dine-In", summary.DineIn);
        AddChannelRow(ws, ref row, isAr ? "تيك أواي" : "Takeaway", summary.TakeAway);
        AddChannelRow(ws, ref row, isAr ? "توصيل" : "Delivery", summary.Delivery);
        AddChannelRow(ws, ref row, isAr ? "ضيافة" : "Hospitality", summary.Hospitality);
        if (summary.Staff.OrderCount > 0 || summary.Staff.Total > 0)
            AddChannelRow(ws, ref row, isAr ? "وجبات موظفين" : "Staff Meals", summary.Staff);

        row += 2;

        // 4. Sold Items Breakdown
        if (items != null && items.Any())
        {
            ws.Cell(row, 1).Value = isAr ? "تفاصيل مبيعات الأصناف" : "Item Sales Details";
            ws.Range(row, 1, row, 4).Merge();
            StyleSectionHeader(ws.Range(row, 1, row, 4));
            row++;

            string[] itemHeaders = isAr ? new[] { "اسم الصنف", "القسم", "الكمية", "إجمالي المبيعات" } : new[] { "Item Name", "Category", "Quantity", "Total Sales" };
            for (int c = 0; c < itemHeaders.Length; c++)
            {
                ws.Cell(row, c + 1).Value = itemHeaders[c];
                StyleTableHeader(ws.Cell(row, c + 1));
            }
            row++;

            foreach (var itm in items.OrderByDescending(i => i.TotalAmount))
            {
                ws.Cell(row, 1).Value = itm.ItemName;
                ws.Cell(row, 2).Value = itm.CategoryName ?? "-";
                ws.Cell(row, 3).Value = itm.Quantity;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.##";
                ws.Cell(row, 4).Value = itm.TotalAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                StyleDataRow(ws.Range(row, 1, row, 4));
                row++;
            }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 2. Sales Detailed
    public static byte[] ExportSalesDetailed(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "المبيعات التفصيلية" : "Detailed Sales"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير المبيعات التفصيلي للأوردرات والأصناف" : "Detailed Orders and Items Sales Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        var orderList = orders ?? new List<OrderDto>();
        TryAddLogo(ws, 12);

        var completed = orderList.Where(o => o.OrderState == "Completed").ToList();
        decimal totalRevenue = completed.Sum(o => o.GrandTotal ?? 0);
        int totalOrders = orderList.Count;
        int completedCount = completed.Count;
        int voidCount = orderList.Count(o => o.OrderState == "Voided");
        decimal avgOrder = completedCount > 0 ? totalRevenue / completedCount : 0;

        ws.Cell(5, 1).Value = isAr ? "إجمالي الإيرادات" : "Total Revenue";
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 1).Style.Fill.BackgroundColor = PrimaryHeaderBg;
        ws.Cell(5, 1).Style.Font.FontColor = XLColor.White;
        ws.Cell(5, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(6, 1).Value = totalRevenue;
        ws.Cell(6, 1).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(6, 1).Style.Font.Bold = true;
        ws.Cell(6, 1).Style.Font.FontSize = 12;
        ws.Cell(6, 1).Style.Font.FontColor = XLColor.FromHtml("#15803D");
        ws.Cell(6, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(5, 1, 5, 2).Merge();
        ws.Range(6, 1, 6, 2).Merge();

        ws.Cell(5, 3).Value = isAr ? "إجمالي الطلبات" : "Total Orders";
        ws.Cell(5, 3).Style.Font.Bold = true;
        ws.Cell(5, 3).Style.Fill.BackgroundColor = PrimaryHeaderBg;
        ws.Cell(5, 3).Style.Font.FontColor = XLColor.White;
        ws.Cell(5, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(6, 3).Value = totalOrders;
        ws.Cell(6, 3).Style.Font.Bold = true;
        ws.Cell(6, 3).Style.Font.FontSize = 12;
        ws.Cell(6, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(5, 3, 5, 4).Merge();
        ws.Range(6, 3, 6, 4).Merge();

        ws.Cell(5, 5).Value = isAr ? "الطلبات المكتملة" : "Completed";
        ws.Cell(5, 5).Style.Font.Bold = true;
        ws.Cell(5, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
        ws.Cell(5, 5).Style.Font.FontColor = XLColor.White;
        ws.Cell(5, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(6, 5).Value = completedCount;
        ws.Cell(6, 5).Style.Font.Bold = true;
        ws.Cell(6, 5).Style.Font.FontSize = 12;
        ws.Cell(6, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(5, 5, 5, 6).Merge();
        ws.Range(6, 5, 6, 6).Merge();

        ws.Cell(5, 7).Value = isAr ? "الطلبات الملغاة" : "Voided";
        ws.Cell(5, 7).Style.Font.Bold = true;
        ws.Cell(5, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#DC2626");
        ws.Cell(5, 7).Style.Font.FontColor = XLColor.White;
        ws.Cell(5, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(6, 7).Value = voidCount;
        ws.Cell(6, 7).Style.Font.Bold = true;
        ws.Cell(6, 7).Style.Font.FontSize = 12;
        ws.Cell(6, 7).Style.Font.FontColor = XLColor.Red;
        ws.Cell(6, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(5, 7, 5, 8).Merge();
        ws.Range(6, 7, 6, 8).Merge();

        ws.Cell(5, 9).Value = isAr ? "متوسط الطلب" : "Avg Order";
        ws.Cell(5, 9).Style.Font.Bold = true;
        ws.Cell(5, 9).Style.Fill.BackgroundColor = PrimaryHeaderBg;
        ws.Cell(5, 9).Style.Font.FontColor = XLColor.White;
        ws.Cell(5, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(6, 9).Value = avgOrder;
        ws.Cell(6, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(6, 9).Style.Font.Bold = true;
        ws.Cell(6, 9).Style.Font.FontSize = 12;
        ws.Cell(6, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(5, 9, 5, 10).Merge();
        ws.Range(6, 9, 6, 10).Merge();

        int row = 8;
        string[] headers = isAr
            ? new[] { "رقم الطلب", "النوع", "الحالة", "التاريخ والوقت", "الكاشير/الموظف", "اسم الصنف", "الإضافات/الخيارات", "ملاحظة الصنف", "الكمية", "سعر الوحدة", "الإجمالي", "ملاحظة الطلب" }
            : new[] { "Order #", "Type", "Status", "Date & Time", "Cashier/Staff", "Item Name", "Attributes/Add-ons", "Item Note", "Quantity", "Unit Price", "Total", "Order Notice" };

        int tableHeaderRow = row;
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var order in orderList.OrderBy(o => o.OrderDate))
        {
            if (order.OrderDetails != null && order.OrderDetails.Any())
            {
                foreach (var item in order.OrderDetails)
                {
                    string attrs = "";
                    if (item.Attributes != null && item.Attributes.Any())
                    {
                        attrs = string.Join(", ", item.Attributes.Select(a => $"{a.Name}" + (a.ExtraPrice > 0 ? $" (+{a.ExtraPrice:0.##})" : "")));
                    }

                    ws.Cell(row, 1).Value = order.OrderId;
                    ws.Cell(row, 2).Value = order.OrderType;
                    ws.Cell(row, 3).Value = order.OrderState;
                    ws.Cell(row, 4).Value = order.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    ws.Cell(row, 5).Value = order.CashierName ?? order.StaffName ?? "-";
                    ws.Cell(row, 6).Value = isAr ? (item.NameAr ?? item.Name) : (item.Name ?? item.NameAr);
                    ws.Cell(row, 7).Value = attrs;
                    ws.Cell(row, 8).Value = item.LineComment ?? "";
                    ws.Cell(row, 9).Value = item.Quantity;
                    ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.##";
                    ws.Cell(row, 10).Value = item.Price ?? 0;
                    ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 11).Value = (item.Quantity * (item.Price ?? 0));
                    ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 12).Value = order.OrderNotice ?? "";

                    StyleDataRow(ws.Range(row, 1, row, headers.Length));
                    row++;
                }
            }
            else
            {
                ws.Cell(row, 1).Value = order.OrderId;
                ws.Cell(row, 2).Value = order.OrderType;
                ws.Cell(row, 3).Value = order.OrderState;
                ws.Cell(row, 4).Value = order.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                ws.Cell(row, 5).Value = order.CashierName ?? "-";
                ws.Cell(row, 6).Value = "-";
                ws.Cell(row, 7).Value = "";
                ws.Cell(row, 8).Value = "";
                ws.Cell(row, 9).Value = 1;
                ws.Cell(row, 10).Value = order.GrandTotal ?? 0;
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 11).Value = order.GrandTotal ?? 0;
                ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 12).Value = order.OrderNotice ?? "";

                StyleDataRow(ws.Range(row, 1, row, headers.Length));
                row++;
            }
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 3. Hospitality
    public static byte[] ExportHospitality(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "الضيافات" : "Hospitality"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير طلبات الضيافة التفصيلي" : "Hospitality Orders Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 9);

        var list = orders ?? new List<OrderDto>();
        int row = 5;

        ws.Cell(row, 1).Value = isAr ? "ملخص الضيافات" : "Hospitality Summary";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد طلبات الضيافة" : "Total Hospitality Orders", list.Count, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي القيمة التقديرية" : "Total Value", list.Sum(o => o.GrandTotal ?? 0), XLColor.Green, isBold: true);
        row += 2;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "رقم الطلب", "التاريخ والوقت", "الكاشير", "المعتمد/المسؤول", "سبب الضيافة", "اسم الصنف", "الكمية", "سعر الوحدة", "الإجمالي" }
            : new[] { "Order#", "Date & Time", "Cashier", "Authorized By", "Reason", "Item Name", "Qty", "Unit Price", "Total" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var o in list)
        {
            var items = o.OrderDetails ?? new List<TableItem>();
            if (items.Any())
            {
                foreach (var itm in items)
                {
                    ws.Cell(row, 1).Value = o.OrderId;
                    ws.Cell(row, 2).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    ws.Cell(row, 3).Value = o.CashierName ?? "-";
                    ws.Cell(row, 4).Value = o.DiscountByName ?? o.DiscountBy ?? "-";
                    ws.Cell(row, 5).Value = o.DiscountReason ?? o.DiscountType ?? "-";
                    ws.Cell(row, 6).Value = isAr ? (itm.NameAr ?? itm.Name) : (itm.Name ?? itm.NameAr);
                    ws.Cell(row, 7).Value = itm.Quantity;
                    ws.Cell(row, 8).Value = itm.Price ?? 0; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 9).Value = (itm.Quantity * (itm.Price ?? 0)); ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                    StyleDataRow(ws.Range(row, 1, row, headers.Length));
                    row++;
                }
            }
            else
            {
                ws.Cell(row, 1).Value = o.OrderId;
                ws.Cell(row, 2).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                ws.Cell(row, 3).Value = o.CashierName ?? "-";
                ws.Cell(row, 4).Value = o.DiscountByName ?? o.DiscountBy ?? "-";
                ws.Cell(row, 5).Value = o.DiscountReason ?? o.DiscountType ?? "-";
                ws.Cell(row, 6).Value = "-";
                ws.Cell(row, 7).Value = 1;
                ws.Cell(row, 8).Value = o.GrandTotal ?? 0; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 9).Value = o.GrandTotal ?? 0; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                StyleDataRow(ws.Range(row, 1, row, headers.Length));
                row++;
            }
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 4. Staff Sales
    public static byte[] ExportStaffSales(List<OrderDto> orders, string staffName, string staffType, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "مبيعات الموظفين" : "Staff Sales"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? $"تقرير مبيعات وأداء الموظف: {staffName}" : $"Staff Sales & Performance Report: {staffName}";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        var list = orders ?? new List<OrderDto>();
        int row = 5;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "رقم الطلب", "نوع الطلب", "التاريخ والوقت", "الحالة", "إجمالي الأصناف", "الخصم", "الصافي الإجمالي" }
            : new[] { "Order#", "Type", "Date/Time", "Status", "Subtotal", "Discount", "Grand Total" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var o in list)
        {
            ws.Cell(row, 1).Value = o.OrderId;
            ws.Cell(row, 2).Value = o.OrderType ?? "-";
            ws.Cell(row, 3).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
            ws.Cell(row, 4).Value = o.OrderState ?? "-";
            ws.Cell(row, 5).Value = o.SubTotal ?? 0; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = o.TotalOrderDiscount ?? o.TotalDiscount ?? 0; ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Value = o.GrandTotal ?? 0; ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 7).Style.Font.Bold = true;
            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 5. Detailed Voids (The user's reported report!)
    public static byte[] ExportDetailedVoids(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "المحذوفات التفصيلية" : "Detailed Voids"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير المحذوفات التفصيلي للأصناف والطلبات" : "Detailed Voids & Cancellations Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 17);

        int row = 5;

        var uniqueOrders = (orders ?? new List<OrderDto>()).GroupBy(o => o.OrderId).Select(g => g.First()).ToList();
        int totalOrders = uniqueOrders.Count;
        int fullVoids = uniqueOrders.Count(o => o.OrderState == "Voided");
        int partialVoids = uniqueOrders.Count(o => o.OrderState != "Voided");
        decimal grandVoidAmount = uniqueOrders.Sum(o => (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (o.OrderState == "Voided" ? (o.GrandTotal ?? 0) : 0));

        ws.Cell(row, 1).Value = isAr ? "ملخص المحذوفات" : "Voids Summary";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد الطلبات الملغاة" : "Total Voided Orders", totalOrders, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إلغاء كامل" : "Full Voids", fullVoids, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إلغاء جزئي" : "Partial Voids", partialVoids, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي المبالغ الملغاة" : "Total Voided Amount", grandVoidAmount, XLColor.Red, isBold: true);

        row += 2;

        string[] headers = isAr
            ? new[] { "رقم الطلب", "نوع الطلب", "الحالة", "تاريخ الطلب", "الكاشير", "تم الحذف بواسطة", "وقت الحذف", "سبب الإلغاء", "قبل الإلغاء", "المبلغ الملغي", "بعد الإلغاء", "اسم الصنف", "الكمية الملغاة", "سعر الوحدة", "إجمالي الصنف", "سبب حذف الصنف", "تم حذف الصنف بواسطة" }
            : new[] { "Order#", "Type", "Status", "Order Date", "Cashier", "Voided By", "Void Time", "Reason", "Before", "Voided", "After", "Item Name", "Void Qty", "Unit Price", "Item Total", "Item Reason", "Item Voided By" };

        int tableHeaderRow = row;
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var o in uniqueOrders)
        {
            bool isFullVoid = o.OrderState == "Voided";
            string statusStr = isFullVoid ? (isAr ? "إلغاء كامل" : "Full Void") : (isAr ? "إلغاء جزئي" : "Partial Void");
            decimal voidedAmt = (o.VoidAmount ?? 0) > 0 ? o.VoidAmount.Value : (isFullVoid ? (o.GrandTotal ?? 0) : 0);
            decimal beforeAmt = isFullVoid ? (voidedAmt > 0 ? voidedAmt : (o.GrandTotal ?? o.SubTotal ?? 0)) : ((o.GrandTotal ?? 0) + voidedAmt);
            decimal afterAmt = isFullVoid ? 0 : (o.GrandTotal ?? 0);

            var items = (o.OrderDetails ?? new List<TableItem>())
                .Where(i => i.IsVoided || (i.VoidAmount ?? 0) > 0 || (i.TotalVoidAmount ?? 0) > 0)
                .ToList();

            if (!items.Any() && isFullVoid)
            {
                items = (o.OrderDetails ?? new List<TableItem>()).ToList();
            }

            if (!items.Any())
            {
                ws.Cell(row, 1).Value = o.OrderId;
                ws.Cell(row, 2).Value = o.OrderType ?? "-";
                ws.Cell(row, 3).Value = statusStr;
                ws.Cell(row, 4).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                ws.Cell(row, 5).Value = o.CashierName ?? o.StaffName ?? "-";
                ws.Cell(row, 6).Value = o.VoidByName ?? "-";
                ws.Cell(row, 7).Value = o.VoidTime?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                ws.Cell(row, 8).Value = o.VoidReason ?? "-";
                ws.Cell(row, 9).Value = beforeAmt; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 10).Value = voidedAmt; ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 10).Style.Font.FontColor = XLColor.Red;
                ws.Cell(row, 11).Value = afterAmt; ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 12).Value = "-";
                ws.Cell(row, 13).Value = "-";
                ws.Cell(row, 14).Value = "-";
                ws.Cell(row, 15).Value = "-";
                ws.Cell(row, 16).Value = "-";
                ws.Cell(row, 17).Value = "-";
                StyleDataRow(ws.Range(row, 1, row, headers.Length));
                row++;
            }
            else
            {
                foreach (var itm in items)
                {
                    decimal iQty = (itm.VoidAmount ?? 0) > 0 ? itm.VoidAmount.Value : itm.Quantity;
                    decimal iPrice = itm.Price ?? 0;
                    decimal iTotal = (itm.TotalVoidAmount ?? 0) > 0 ? itm.TotalVoidAmount.Value : (iQty * iPrice);

                    ws.Cell(row, 1).Value = o.OrderId;
                    ws.Cell(row, 2).Value = o.OrderType ?? "-";
                    ws.Cell(row, 3).Value = statusStr;
                    ws.Cell(row, 4).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    ws.Cell(row, 5).Value = o.CashierName ?? o.StaffName ?? "-";
                    ws.Cell(row, 6).Value = o.VoidByName ?? "-";
                    ws.Cell(row, 7).Value = o.VoidTime?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    ws.Cell(row, 8).Value = o.VoidReason ?? "-";
                    ws.Cell(row, 9).Value = beforeAmt; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 10).Value = voidedAmt; ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 10).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 11).Value = afterAmt; ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 12).Value = isAr ? (itm.NameAr ?? itm.Name ?? "-") : (itm.Name ?? itm.NameAr ?? "-");
                    ws.Cell(row, 13).Value = iQty;
                    ws.Cell(row, 14).Value = iPrice; ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 15).Value = iTotal; ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 15).Style.Font.Bold = true;
                    ws.Cell(row, 16).Value = !string.IsNullOrWhiteSpace(itm.VoidReason) ? itm.VoidReason : (o.VoidReason ?? "-");
                    ws.Cell(row, 17).Value = !string.IsNullOrWhiteSpace(itm.VoidByName) ? itm.VoidByName : (o.VoidByName ?? "-");
                    StyleDataRow(ws.Range(row, 1, row, headers.Length));
                    row++;
                }
            }
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 6. Branch Comparison
    public static byte[] ExportBranchComparison(List<BranchComparisonItem> branches, string companyName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "مقارنة الفروع" : "Branch Comparison"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = companyName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير مقارنة مبيعات وأداء الفروع" : "Branch Sales Comparison Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 10);

        var list = branches ?? new List<BranchComparisonItem>();
        int row = 5;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "الترتيب", "اسم الفرع", "عدد الطلبات", "إجمالي المبيعات", "متوسط الطلب", "النسبة %", "تيك أواي", "توصيل", "صالة" }
            : new[] { "Rank", "Branch Name", "Orders", "Total Sales", "Avg Ticket", "% Share", "Takeaway", "Delivery", "Dine-In" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        int idx = 1;
        foreach (var b in list.OrderByDescending(x => x.TotalSales))
        {
            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = b.BranchName;
            ws.Cell(row, 3).Value = b.OrderCount;
            ws.Cell(row, 4).Value = b.TotalSales; ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = b.AverageOrderValue; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = $"{b.PercentageOfTotal:0.#}%";
            ws.Cell(row, 7).Value = b.TakeawayTotal; ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Value = b.DeliveryTotal; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Value = b.DineInTotal; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 7. Delivery Bonus
    public static byte[] ExportDeliveryBonus(List<DriverBonusItem> drivers, List<DeliveryOrderRecord> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "بونص الطيارين" : "Delivery Fees & Bonus"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير خدمة التوصيل وبونص الطيارين" : "Delivery Fees & Drivers Bonus Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 8);

        var list = drivers ?? new List<DriverBonusItem>();
        int row = 5;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "الترتيب", "اسم الطيار", "عدد الطلبات", "إجمالي المبيعات", "رسوم التوصيل", "إجمالي البونص", "متوسط البونص", "النسبة %" }
            : new[] { "Rank", "Driver Name", "Orders", "Sales", "Delivery Fees", "Total Bonus", "Avg Bonus", "% Share" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        int idx = 1;
        foreach (var d in list.OrderByDescending(x => x.TotalBonus))
        {
            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = d.DriverName;
            ws.Cell(row, 3).Value = d.OrderCount;
            ws.Cell(row, 4).Value = d.TotalSales; ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = d.DeliveryFees; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = d.TotalBonus; ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = d.AverageBonusPerOrder; ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Value = $"{d.BonusPercentage:0.#}%";
            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 8. Cashier Comparison
    public static byte[] ExportCashierComparison(List<CashierComparisonItem> cashiers, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "مقارنة الكاشيرية" : "Cashier Comparison"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير مقارنة أداء الكاشيرية" : "Cashier Performance Comparison Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 12);

        var list = cashiers ?? new List<CashierComparisonItem>();
        int row = 5;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "الترتيب", "اسم الكاشير", "إجمالي الطلبات", "المكتملة", "إجمالي المبيعات", "نقدي", "فيزا", "إجمالي الخصم", "إجمالي الإلغاء", "صافي المبيعات", "متوسط الطلب", "النسبة %" }
            : new[] { "Rank", "Cashier Name", "Total Orders", "Completed", "Total Sales", "Cash", "Credit", "Discount", "Void", "Net Sales", "Avg Ticket", "% Share" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        int idx = 1;
        foreach (var c in list.OrderByDescending(x => x.TotalSales))
        {
            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = c.CashierName;
            ws.Cell(row, 3).Value = c.OrderCount;
            ws.Cell(row, 4).Value = c.CompletedCount;
            ws.Cell(row, 5).Value = c.TotalSales; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = c.CashAmount; ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Value = c.CreditAmount; ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Value = c.TotalDiscount; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Value = c.TotalVoid; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Value = c.NetSales; ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 10).Style.Font.Bold = true;
            ws.Cell(row, 11).Value = c.AverageTicket; ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 12).Value = $"{c.PercentageOfTotal:0.#}%";
            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 9. Void Summary
    public static byte[] ExportVoidSummary(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "ملخص المحذوفات" : "Void Summary"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير ملخص المحذوفات والإلغاءات" : "Void Orders Summary Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 8);

        var list = orders ?? new List<OrderDto>();
        int row = 5;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "رقم الطلب", "النوع", "الحالة", "التاريخ والوقت", "الكاشير", "تم الحذف بواسطة", "السبب", "المبلغ الملغي" }
            : new[] { "Order#", "Type", "Status", "Date/Time", "Cashier", "Voided By", "Reason", "Void Amount" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var o in list)
        {
            ws.Cell(row, 1).Value = o.OrderId;
            ws.Cell(row, 2).Value = o.OrderType ?? "-";
            ws.Cell(row, 3).Value = o.OrderState ?? "-";
            ws.Cell(row, 4).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
            ws.Cell(row, 5).Value = o.CashierName ?? "-";
            ws.Cell(row, 6).Value = o.VoidByName ?? "-";
            ws.Cell(row, 7).Value = o.VoidReason ?? "-";
            ws.Cell(row, 8).Value = o.VoidAmount ?? o.GrandTotal ?? 0; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 8).Style.Font.Bold = true;
            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 10. Detailed Discounts
    public static byte[] ExportDetailedDiscounts(List<OrderDto> orders, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "الخصومات التفصيلية" : "Detailed Discounts"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير الخصومات التفصيلي للأوردرات والأصناف" : "Detailed Discounts Report (Orders & Items)";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 17);

        var orderList = orders ?? new List<OrderDto>();
        int totalOrders = orderList.Count;

        decimal totalDiscountAmount = orderList.Sum(o => {
            decimal d = (o.TotalOrderDiscount ?? 0) + (o.TotalDiscount ?? 0) + (o.DiscountedItems ?? 0);
            if (d <= 0 && o.OrderDetails != null && o.OrderDetails.Any())
            {
                d = o.OrderDetails.Sum(i =>
                    (i.DiscountAmount ?? 0) > 0 ? (i.DiscountAmount.Value * i.Quantity) :
                    ((i.DiscountPercentage ?? 0) > 0 ? (i.Price ?? 0) * (i.DiscountPercentage.Value / 100m) * i.Quantity :
                    ((i.TotalDiscountPrice ?? 0) > 0 ? i.TotalDiscountPrice.Value : 0)));
            }
            return d;
        });

        decimal totalNetSales = orderList.Sum(o => o.GrandTotal ?? 0);
        decimal totalGrossSales = totalNetSales + totalDiscountAmount;
        decimal avgDiscount = totalOrders > 0 ? (totalDiscountAmount / totalOrders) : 0;

        int row = 5;

        ws.Cell(row, 1).Value = isAr ? "ملخص الخصومات" : "Discounts Summary";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد طلبات الخصم" : "Total Discounted Orders", totalOrders, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي قيمة الخصومات" : "Total Discounts Amount", totalDiscountAmount, XLColor.Red, isBold: true);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي المبيعات قبل الخصم" : "Total Gross Before Disc.", totalGrossSales);
        AddTwoColumnRow(ws, ref row, isAr ? "صافي المبيعات بعد الخصم" : "Net Sales After Disc.", totalNetSales, XLColor.Green, isBold: true);
        AddTwoColumnRow(ws, ref row, isAr ? "متوسط الخصم لكل طلب" : "Average Discount / Order", avgDiscount);

        row += 2;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "رقم الطلب", "نوع الطلب", "التاريخ والوقت", "الكاشير", "المسؤول / مانح الخصم", "نوع الخصم", "سبب الخصم", "قبل الخصم", "قيمة الخصم", "نسبة الخصم", "الصافي بعد الخصم", "اسم الصنف", "الكمية", "سعر الوحدة", "خصم الصنف", "إجمالي الصنف", "سبب خصم الصنف / ملاحظة" }
            : new[] { "Order#", "Type", "Date/Time", "Cashier", "Authorized By", "Discount Type", "Discount Reason", "Gross Before", "Discount Amount", "Discount %", "Net After", "Item Name", "Qty", "Unit Price", "Item Discount", "Item Total", "Item Reason/Note" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        foreach (var o in orderList)
        {
            decimal oDisc = (o.TotalOrderDiscount ?? 0) + (o.TotalDiscount ?? 0) + (o.DiscountedItems ?? 0);
            if (oDisc <= 0 && o.OrderDetails != null && o.OrderDetails.Any())
            {
                oDisc = o.OrderDetails.Sum(i =>
                    (i.DiscountAmount ?? 0) > 0 ? (i.DiscountAmount.Value * i.Quantity) :
                    ((i.DiscountPercentage ?? 0) > 0 ? (i.Price ?? 0) * (i.DiscountPercentage.Value / 100m) * i.Quantity :
                    ((i.TotalDiscountPrice ?? 0) > 0 ? i.TotalDiscountPrice.Value : 0)));
            }

            decimal oNet = o.GrandTotal ?? 0;
            decimal oGross = oNet + oDisc;

            var items = o.OrderDetails ?? new List<TableItem>();

            if (!items.Any())
            {
                ws.Cell(row, 1).Value = o.OrderId;
                ws.Cell(row, 2).Value = o.OrderType ?? "-";
                ws.Cell(row, 3).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                ws.Cell(row, 4).Value = o.CashierName ?? o.StaffName ?? "-";
                ws.Cell(row, 5).Value = o.DiscountByName ?? o.DiscountBy ?? "-";
                ws.Cell(row, 6).Value = o.DiscountType ?? "-";
                ws.Cell(row, 7).Value = o.DiscountReason ?? "-";
                ws.Cell(row, 8).Value = oGross; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 9).Value = oDisc; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 9).Style.Font.FontColor = XLColor.Red;
                ws.Cell(row, 10).Value = o.DiscountPercentage.HasValue ? $"{o.DiscountPercentage.Value:0.#}%" : "-";
                ws.Cell(row, 11).Value = oNet; ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 11).Style.Font.Bold = true;
                ws.Cell(row, 12).Value = "-";
                ws.Cell(row, 13).Value = "-";
                ws.Cell(row, 14).Value = "-";
                ws.Cell(row, 15).Value = "-";
                ws.Cell(row, 16).Value = "-";
                ws.Cell(row, 17).Value = "-";
                StyleDataRow(ws.Range(row, 1, row, headers.Length));
                row++;
            }
            else
            {
                foreach (var itm in items)
                {
                    decimal itemDisc = (itm.DiscountAmount ?? 0) > 0 ? (itm.DiscountAmount.Value * itm.Quantity) :
                                       ((itm.DiscountPercentage ?? 0) > 0 ? (itm.Price ?? 0) * (itm.DiscountPercentage.Value / 100m) * itm.Quantity :
                                       ((itm.TotalDiscountPrice ?? 0) > 0 ? itm.TotalDiscountPrice.Value : 0));

                    decimal lineTotal = itm.TotalAfterDiscount ?? itm.Total ?? ((itm.Quantity * (itm.Price ?? 0)) - itemDisc);

                    ws.Cell(row, 1).Value = o.OrderId;
                    ws.Cell(row, 2).Value = o.OrderType ?? "-";
                    ws.Cell(row, 3).Value = o.OrderDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    ws.Cell(row, 4).Value = o.CashierName ?? o.StaffName ?? "-";
                    ws.Cell(row, 5).Value = o.DiscountByName ?? o.DiscountBy ?? "-";
                    ws.Cell(row, 6).Value = o.DiscountType ?? "-";
                    ws.Cell(row, 7).Value = o.DiscountReason ?? "-";
                    ws.Cell(row, 8).Value = oGross; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 9).Value = oDisc; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 9).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 10).Value = o.DiscountPercentage.HasValue ? $"{o.DiscountPercentage.Value:0.#}%" : "-";
                    ws.Cell(row, 11).Value = oNet; ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 11).Style.Font.Bold = true;
                    ws.Cell(row, 12).Value = isAr ? (itm.NameAr ?? itm.Name ?? "-") : (itm.Name ?? itm.NameAr ?? "-");
                    ws.Cell(row, 13).Value = itm.Quantity;
                    ws.Cell(row, 14).Value = itm.Price ?? 0; ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 15).Value = itemDisc; ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 16).Value = lineTotal; ws.Cell(row, 16).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 16).Style.Font.Bold = true;
                    ws.Cell(row, 17).Value = itm.DiscountReason ?? itm.LineComment ?? "-";
                    StyleDataRow(ws.Range(row, 1, row, headers.Length));
                    row++;
                }
            }
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 14. Customer Report (Directory, VIP, Dormant)
    public static byte[] ExportCustomerReport(List<CustomerReportItem> customers, string branchName, string reportTitle, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "سجل العملاء" : "Customers"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = reportTitle;
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 7);

        var list = customers ?? new List<CustomerReportItem>();
        int row = 5;

        ws.Cell(row, 1).Value = isAr ? "ملخص العملاء" : "Customers Summary";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد العملاء" : "Total Customers", list.Count, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد الطلبات" : "Total Orders", list.Sum(c => c.TotalOrders), isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي المشتريات" : "Total Spent", list.Sum(c => c.TotalAmount), XLColor.Green, isBold: true);

        row += 2;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "#", "اسم العميل", "رقم الهاتف", "العنوان", "عدد الطلبات", "إجمالي المشتريات", "تاريخ آخر طلب" }
            : new[] { "#", "Customer Name", "Phone", "Address", "Orders", "Total Spent", "Last Order Date" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        int idx = 1;
        foreach (var c in list)
        {
            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = c.Name;
            ws.Cell(row, 3).Value = c.Phone;
            ws.Cell(row, 4).Value = c.Address;
            ws.Cell(row, 5).Value = c.TotalOrders;
            ws.Cell(row, 6).Value = c.TotalAmount; ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToString("yyyy-MM-dd HH:mm") : "-";

            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // 15. Delivery Areas
    public static byte[] ExportDeliveryAreas(List<DeliveryAreaReportItem> areas, string branchName, DateTime fromDate, DateTime toDate, string lang = "ar")
    {
        bool isAr = (lang ?? "ar").ToLower() == "ar";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SanitizeSheetName(isAr ? "مناطق التوصيل" : "Delivery Areas"));
        ws.RightToLeft = isAr;

        ws.Cell(1, 1).Value = branchName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = isAr ? "تقرير مناطق التوصيل الأكثر طلباً" : "Top Delivery Areas Report";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Style.Font.FontColor = PrimaryHeaderBg;

        ws.Cell(3, 1).Value = (isAr ? "الفترة: من " : "Period: From ") + fromDate.ToString("yyyy-MM-dd") + (isAr ? " إلى " : " To ") + toDate.ToString("yyyy-MM-dd");
        ws.Cell(3, 1).Style.Font.Italic = true;

        TryAddLogo(ws, 6);

        var list = areas ?? new List<DeliveryAreaReportItem>();
        int row = 5;

        decimal grandTotal = list.Sum(a => a.TotalAmount);
        int grandOrders = list.Sum(a => a.TotalOrders);

        ws.Cell(row, 1).Value = isAr ? "ملخص المناطق" : "Areas Summary";
        ws.Range(row, 1, row, 2).Merge();
        StyleSectionHeader(ws.Range(row, 1, row, 2));
        row++;

        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي عدد المناطق" : "Total Zones", list.Count, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي الطلبات" : "Total Orders", grandOrders, isCurrency: false);
        AddTwoColumnRow(ws, ref row, isAr ? "إجمالي الإيرادات" : "Total Revenue", grandTotal, XLColor.Green, isBold: true);

        row += 2;

        int tableHeaderRow = row;
        string[] headers = isAr
            ? new[] { "#", "منطقة التوصيل", "عدد الطلبات", "متوسط الطلب", "إجمالي الإيراد", "النسبة %" }
            : new[] { "#", "Zone Name", "Orders", "Avg Ticket", "Revenue", "% Share" };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            StyleTableHeader(ws.Cell(row, c + 1));
        }
        row++;

        int idx = 1;
        foreach (var a in list.OrderByDescending(x => x.TotalAmount))
        {
            decimal pct = grandTotal > 0 ? (a.TotalAmount / grandTotal * 100) : 0;

            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = a.ZoneName;
            ws.Cell(row, 3).Value = a.TotalOrders;
            ws.Cell(row, 4).Value = a.AverageOrderValue; ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = a.TotalAmount; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00"; ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = $"{pct:0.#}%";

            StyleDataRow(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        if (row > tableHeaderRow + 1)
        {
            try
            {
                var tableRange = ws.Range(tableHeaderRow, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium9;
            }
            catch { }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
