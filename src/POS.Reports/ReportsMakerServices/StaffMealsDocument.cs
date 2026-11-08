using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.Contract.Dtos.ReportingDtos;
using POS.Contract.Dtos.OrderDtos;
using System.Collections.Generic;
using System.Linq;
using System;

namespace POS.Reports.ReportsMakerServices;

public class StaffMealsDocument : IDocument
{
    private readonly List<OrderDto> _orders;
    private readonly DateTime _date;
    private readonly string _branchName;
    private readonly bool _isArabic;
    private readonly bool _isThermal;

    public StaffMealsDocument(List<OrderDto> orders, DateTime date, string branchName, bool isArabic = true, bool isThermal = false)
    {
        _orders = orders ?? new List<OrderDto>();
        _date = date;
        _branchName = branchName;
        _isArabic = isArabic;
        _isThermal = isThermal;
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
                page.Margin(1, Unit.Centimetre);
            }

            page.PageColor(Colors.White);
            
            var baseStyle = TextStyle.Default.FontFamily("Arial", "Noto Sans Arabic").FontSize(_isThermal ? 8 : 10);
            page.DefaultTextStyle(baseStyle);

            if (_isArabic)
                page.ContentFromRightToLeft();

            page.Content().Column(col =>
            {
                col.Spacing(_isThermal ? 3 : 5);
                ComposeHeader(col);
                ComposeTable(col);
                ComposeSummary(col);
                ComposeFooter(col);
            });
        });
    }

    private void ComposeHeader(ColumnDescriptor col)
    {
        // Branch Name (Black, Center)
        col.Item().AlignCenter().Text(_branchName).FontSize(_isThermal ? 11 : 14).Bold().FontColor(Colors.Black);
        
        // Main Title (Black, Center)
        col.Item().AlignCenter().Text(_isArabic ? "تقرير وجبات العاملين" : "Staff Meals Report").FontSize(_isThermal ? 12 : 18).Bold().FontColor(Colors.Black);
        
        // Date & Subtitle
        col.Item().AlignCenter().Text(_isArabic ? $"التاريخ: {_date:dd/MM/yyyy}" : $"Date: {_date:dd/MM/yyyy}").FontSize(_isThermal ? 8 : 11).FontColor(Colors.Grey.Darken3);
        
        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);
    }

    private void ComposeTable(ColumnDescriptor col)
    {
        col.Item().PaddingTop(2).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.2f); // ID
                cd.RelativeColumn(3.5f); // Employee Name
                cd.RelativeColumn(3);    // Meal Name (Item)
                cd.RelativeColumn(1);    // Qty
                cd.RelativeColumn(1.8f); // Time
            });

            t.Header(h =>
            {
                h.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isArabic ? "#" : "ID").Bold();
                h.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isArabic ? "اسم العامل" : "Employee").Bold();
                h.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isArabic ? "الوجبة" : "Meal").Bold();
                h.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isArabic ? "الكمية" : "Qty").Bold();
                h.Cell().Border(1).Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(_isArabic ? "الوقت" : "Time").Bold();
            });

            foreach (var order in _orders)
            {
                t.Cell().Border(1).Padding(2).AlignCenter().Text($"#{order.OrderId}").FontSize(_isThermal ? 7 : 9);
                
                string employeeName = !string.IsNullOrEmpty(order.StaffMealEmployeeName) 
                    ? order.StaffMealEmployeeName 
                    : (order.CustomerName ?? order.TakeAwayCustomerName ?? "System");
                    
                t.Cell().Border(1).Padding(2).AlignCenter().Text(employeeName).FontSize(_isThermal ? 7 : 9).Bold();
                
                // Aggregate items in the order
                var mealNames = order.OrderDetails != null 
                    ? string.Join(", ", order.OrderDetails.Select(d => _isArabic ? (d.NameAr ?? d.Name) : (d.Name ?? d.NameAr)))
                    : "-";
                
                var totalQty = order.OrderDetails?.Sum(d => d.Quantity) ?? 0;

                t.Cell().Border(1).Padding(2).AlignCenter().Text(mealNames).FontSize(_isThermal ? 7 : 8);
                t.Cell().Border(1).Padding(2).AlignCenter().Text(totalQty.ToString("G29")).FontSize(_isThermal ? 7 : 9);
                t.Cell().Border(1).Padding(2).AlignCenter().Text(order.OrderDate?.ToString("HH:mm") ?? "-").FontSize(_isThermal ? 7 : 9);
            }
        });
    }

    private void ComposeSummary(ColumnDescriptor col)
    {
        col.Item().PaddingTop(10).Column(sc => {
            sc.Item().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text(_isArabic ? "ملخص الوجبات" : "Meals Summary").Bold();
            
            // Grouping by item to show aggregate counts
            var itemSummary = _orders.SelectMany(o => o.OrderDetails ?? new List<POS.Contract.Models.TableItem>())
                .GroupBy(d => d.NameAr ?? d.Name)
                .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            sc.Item().Table(st => {
                st.ColumnsDefinition(cd => {
                    cd.RelativeColumn(4);
                    cd.RelativeColumn(1);
                });

                foreach(var item in itemSummary)
                {
                    st.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(item.Name);
                    st.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text(item.Qty.ToString("G29")).Bold();
                }

                // Overall total
                st.Cell().Padding(2).AlignRight().Text(_isArabic ? "الإجمالي الكلي" : "Grand Total").Bold();
                st.Cell().Padding(2).AlignCenter().Text(itemSummary.Sum(x => x.Qty).ToString("G29")).Bold();
            });
        });
    }

    private void ComposeFooter(ColumnDescriptor col)
    {
        col.Item().PaddingTop(10).AlignCenter().Text(text =>
        {
            text.Span(_isArabic ? "طبع في: " : "Printed at: ").FontSize(7);
            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(7);
        });
    }
}
