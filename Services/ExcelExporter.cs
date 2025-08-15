using ClosedXML.Excel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ADLMRateGen.Services
{
    public static class ExcelExporter
    {
        public readonly struct ExportSheet
        {
            public string Name { get; }
            public IEnumerable Rows { get; }

            public ExportSheet(string name, IEnumerable rows)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Sheet" : name.Trim();
                Rows = rows ?? Array.Empty<object>();
            }
        }

        public static void ExportWorkbook(IReadOnlyList<ExportSheet> sheets, string filePath)
        {
            if (sheets == null || sheets.Count == 0)
                throw new ArgumentException("No sheets to export.");

            using var wb = new XLWorkbook();

            // Workbook metadata
            wb.Properties.Author = "ADLM Rate Gen";
            wb.Properties.LastModifiedBy = "ADLM Rate Gen";
            wb.Properties.Company = "ADLM Studio";
            wb.Properties.Title = "ADLM Rates Export";

            foreach (var sheet in sheets)
            {
                var ws = wb.Worksheets.Add(SafeSheetName(sheet.Name));
                AddBranding(ws); // <- footer “engraving”

                var rows = sheet.Rows.Cast<object>().ToList();
                if (rows.Count == 0)
                {
                    ws.Cell(1, 1).SetValue("No data");
                    continue;
                }

                var t = rows[0].GetType();
                bool looksLikeCustomRate =
                    Has(t, "Title") &&
                    Has(t, "Description") &&
                    Has(t, "OverheadPercent") &&
                    Has(t, "ProfitPercent") &&
                    Has(t, "GrandTotal");

                if (looksLikeCustomRate)
                    WriteCustomRates(ws, rows);
                else
                    WriteGeneric(ws, rows);
            }

            wb.SaveAs(filePath);
        }

        private static void AddBranding(IXLWorksheet ws)
        {
            var stamp = $"Generated from ADLM Rate Gen  ·  {DateTime.Now:yyyy-MM-dd HH:mm}";
            // ClosedXML: use Footer.Center (not CenterFooter)
            var hf = ws.PageSetup.Footer.Center.AddText(stamp);
            hf.Italic = true;
            hf.FontSize = 8;
            hf.FontName = "Calibri";
            // Optional: adjust footer margin a bit
            ws.PageSetup.Margins.Footer = 0.3; // inches
        }

        private static bool Has(Type t, string prop) => t.GetProperty(prop) != null;

        private static void WriteGeneric(IXLWorksheet ws, IReadOnlyList<object> rows)
        {
            ws.Cell(1, 1).SetValue("S/N");
            ws.Cell(1, 2).SetValue("Description");
            ws.Cell(1, 3).SetValue("Total");

            var header = ws.Range(1, 1, 1, 3);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            var (getSn, getDesc, getTotal) = MakeAccessors(rows);

            int r = 2;
            int fallbackSn = 1;
            foreach (var row in rows)
            {
                var sn = getSn(row) ?? fallbackSn;
                var desc = getDesc(row) ?? string.Empty;
                var tot = getTotal(row);

                ws.Cell(r, 1).SetValue(sn);
                ws.Cell(r, 2).SetValue(desc);
                if (tot.HasValue) ws.Cell(r, 3).SetValue(tot.Value);

                r++;
                fallbackSn++;
            }

            ws.Column(3).Style.NumberFormat.Format = "#,##0.00";
            ws.Columns().AdjustToContents();
            if (ws.Column(2).Width < 50) ws.Column(2).Width = 50;
        }

        private static void WriteCustomRates(IXLWorksheet ws, IReadOnlyList<object> rows)
        {
            ws.Cell(1, 1).SetValue("Title");
            ws.Cell(1, 2).SetValue("Description");
            ws.Cell(1, 3).SetValue("Overhead (%)");
            ws.Cell(1, 4).SetValue("Profit (%)");
            ws.Cell(1, 5).SetValue("Grand Total");
            ws.Cell(1, 6).SetValue("Created Date");

            var header = ws.Range(1, 1, 1, 6);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            int r = 2;
            foreach (var o in rows)
            {
                var t = o.GetType();

                ws.Cell(r, 1).SetValue(t.GetProperty("Title")?.GetValue(o)?.ToString() ?? "");
                ws.Cell(r, 2).SetValue(t.GetProperty("Description")?.GetValue(o)?.ToString() ?? "");

                var ov = ToDouble(t.GetProperty("OverheadPercent")?.GetValue(o));
                var pf = ToDouble(t.GetProperty("ProfitPercent")?.GetValue(o));
                var gt = ToDouble(t.GetProperty("GrandTotal")?.GetValue(o));
                var cd = t.GetProperty("CreatedDate")?.GetValue(o);

                if (ov.HasValue) ws.Cell(r, 3).SetValue(ov.Value);
                if (pf.HasValue) ws.Cell(r, 4).SetValue(pf.Value);
                if (gt.HasValue) ws.Cell(r, 5).SetValue(gt.Value);

                if (cd is DateTime dt) ws.Cell(r, 6).SetValue(dt);

                r++;
            }

            ws.Column(5).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            ws.Columns().AdjustToContents();
            if (ws.Column(2).Width < 50) ws.Column(2).Width = 50;
        }

        private static (Func<object, int?> getSn, Func<object, string?> getDesc, Func<object, double?> getTotal)
            MakeAccessors(IReadOnlyList<object> sample)
        {
            var type = sample[0].GetType();

            var pSn = type.GetProperty("ItemNo") ?? type.GetProperty("SNo") ?? type.GetProperty("Sn") ?? type.GetProperty("Index");
            var pDesc = type.GetProperty("Description") ?? type.GetProperty("Name") ?? type.GetProperty("Title") ?? type.GetProperty("MaterialName");
            var pTotal = type.GetProperty("TotalCost") ?? type.GetProperty("TotalPrice") ?? type.GetProperty("GrandTotal") ?? type.GetProperty("Total");

            int? ToIntLocal(object? v) { try { return v == null ? (int?)null : Convert.ToInt32(v); } catch { return null; } }
            double? ToDoubleLocal(object? v) { try { return v == null ? (double?)null : Convert.ToDouble(v); } catch { return null; } }

            int? getSn(object o) => ToIntLocal(pSn?.GetValue(o));
            string? getDesc(object o) => pDesc?.GetValue(o)?.ToString();
            double? getTotal(object o) => ToDoubleLocal(pTotal?.GetValue(o));

            return (getSn, getDesc, getTotal);
        }

        private static string SafeSheetName(string name)
        {
            var bad = new HashSet<char>(System.IO.Path.GetInvalidFileNameChars().Concat(new[] { '[', ']', '*', '?', '/', '\\' }));
            var clean = new string(name.Where(c => !bad.Contains(c)).ToArray());
            if (string.IsNullOrWhiteSpace(clean)) clean = "Sheet";
            return clean.Length > 31 ? clean[..31] : clean;
        }

        private static double? ToDouble(object? v)
        {
            try { return v == null ? (double?)null : Convert.ToDouble(v); }
            catch { return null; }
        }
    }
}
