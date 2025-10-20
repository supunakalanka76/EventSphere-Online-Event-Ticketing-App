using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Table;
using OfficeOpenXml.Style;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace EventSphere.Helpers
{
    public static class ExportHelper
    {
        // EXCEL EXPORT (EPPlus 8+)
        public static void ExportToExcel<T>(IEnumerable<T> data, string filePath)
        {
            // Set the license context for EPPlus 8+ (non-commercial use)
            ExcelPackage.License.SetNonCommercialPersonal("EvenrSphere");

            var list = data?.ToList() ?? new List<T>();
            if (!list.Any())
                throw new Exception("No data available to export.");

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Report");
                ws.Cells["A1"].LoadFromCollection(list, true, TableStyles.Light9);

                // Format header
                using (var header = ws.Cells[1, 1, 1, ws.Dimension.End.Column])
                {
                    header.Style.Font.Bold = true;
                    header.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                    header.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Add totals for numeric columns
                int lastRow = ws.Dimension.End.Row;
                int lastCol = ws.Dimension.End.Column;
                int totalRow = lastRow + 2;

                ws.Cells[totalRow, 1].Value = "Totals:";
                ws.Cells[totalRow, 1].Style.Font.Bold = true;

                for (int col = 2; col <= lastCol; col++)
                {
                    if (double.TryParse(ws.Cells[2, col].Text, out _))
                    {
                        string colLetter = ws.Cells[1, col].Address.Substring(0, 1);
                        ws.Cells[totalRow, col].Formula = $"SUM({colLetter}2:{colLetter}{lastRow})";
                        ws.Cells[totalRow, col].Style.Font.Bold = true;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // Save file
                FileInfo file = new FileInfo(filePath);
                package.SaveAs(file);
            }
        }

        // PDF EXPORT (iTextSharp)
        public static void ExportToPdf<T>(IEnumerable<T> data, string filePath, string title)
        {
            var list = data?.ToList() ?? new List<T>();
            if (!list.Any())
                throw new Exception("No data available to export.");

            var props = typeof(T).GetProperties();

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                var doc = new Document(PageSize.A4.Rotate(), 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, stream);
                doc.Open();

                // Title Section
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
                var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
                doc.Add(new Paragraph(title, titleFont) { Alignment = Element.ALIGN_CENTER });
                doc.Add(new Paragraph($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}", subtitleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                });

                // Table
                PdfPTable table = new PdfPTable(props.Length)
                {
                    WidthPercentage = 100
                };

                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                var headerBg = new BaseColor(79, 129, 189);

                // Header Row
                foreach (var prop in props)
                {
                    var cell = new PdfPCell(new Phrase(prop.Name, headerFont))
                    {
                        BackgroundColor = headerBg,
                        Padding = 6,
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    table.AddCell(cell);
                }

                // Data Rows
                foreach (var item in list)
                {
                    foreach (var prop in props)
                    {
                        var value = prop.GetValue(item, null)?.ToString() ?? "";
                        var cell = new PdfPCell(new Phrase(value, cellFont))
                        {
                            Padding = 5,
                            HorizontalAlignment = Element.ALIGN_LEFT
                        };
                        table.AddCell(cell);
                    }
                }

                doc.Add(table);

                // Footer
                doc.Add(new Paragraph("\nGenerated by EventSphere", subtitleFont)
                {
                    Alignment = Element.ALIGN_RIGHT
                });

                doc.Close();
            }
        }
    }
}