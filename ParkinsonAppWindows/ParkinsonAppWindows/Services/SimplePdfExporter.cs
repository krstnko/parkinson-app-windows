using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ParkinsonAppWindows.Helpers;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.ViewModels;

namespace ParkinsonAppWindows.Services
{
    public static class SimplePdfExporter
    {
        public static void Export(List<DrawingPoint> points, DrawingStats stats, string filePath, DrawingParameter thicknessParam, DrawingParameter colorParam, bool isDarkMode, bool showGrid, string fileNameOnly)
        {
            if (points == null || points.Count < 2) return;
            double width = 1024.0; double height = 1024.0;
            var sb = InitPdf(width, height, out int contentId, out StringBuilder cs, isDarkMode);
            DrawGraphInPdfCell(cs, points, stats, thicknessParam, colorParam, 0, 0, width, height, height, isDarkMode, showGrid, fileNameOnly, true);
            FinalizePdf(sb, cs, contentId, filePath);
        }

        public static void ExportAll(IEnumerable<ExportViewItem> items, string filePath, bool isDarkMode, bool showGrid)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            double docW = 842.0; // A4 Landscape width (pt)
            double docH = 595.0; // A4 Landscape height (pt)

            int count = itemList.Count;
            int cols = 2;
            int rows = (int)Math.Ceiling((double)count / cols);

            if (rows * 350.0 > docH) docH = rows * 350.0;

            double cellW = docW / cols;
            double cellH = docH / rows;

            var sb = InitPdf(docW, docH, out int contentId, out StringBuilder cs, isDarkMode);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols; int row = i / cols;
                double cellX = col * cellW;
                double cellY = row * cellH;

                var item = itemList[i];
                DrawGraphInPdfCell(cs, item.Session.DrawingPoints, item.Session.Stats, item.ThicknessParam, item.ColorParam, cellX, cellY, cellW, cellH, docH, isDarkMode, showGrid, item.Session.FileName, false);
            }

            FinalizePdf(sb, cs, contentId, filePath);
        }

        private static void DrawGraphInPdfCell(StringBuilder cs, List<DrawingPoint> points, DrawingStats stats, DrawingParameter thickP, DrawingParameter colorP, double cellX, double cellY, double cellW, double cellH, double docH, bool isDark, bool showGrid, string title, bool isSingle)
        {
            cs.AppendLine("q"); // Save State

            double padding = isSingle ? 40.0 : 20.0;
            double footerH = 30.0;

            double availW = cellW - (padding * 2);
            double availH = cellH - (padding * 2) - footerH;

            double side = Math.Min(availW, availH);
            double squareX = cellX + padding + (availW - side) / 2;
            double squareY = cellY + padding + (availH - side) / 2;

            double margin = 35.0; 
            double plotW = side - margin;
            double plotH = side - margin;
            double plotX = squareX + margin;
            double plotY = squareY;

            if (plotW <= 0 || plotH <= 0) { cs.AppendLine("Q"); return; }

            double minX = points.Min(p => p.X); double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y); double maxY = points.Max(p => p.Y);

            double dataMax = Math.Max(maxX - minX, maxY - minY);
            double scale = plotW / Math.Max(dataMax, 1.0);

            double offsetX = plotX + (plotW - (maxX - minX) * scale) / 2;
            double offsetY = plotY + (plotH - (maxY - minY) * scale) / 2;

            // PDF Coordinates (Y flipped)
            double Tx(double val) => (val - minX) * scale + offsetX;
            double Ty(double val) => docH - ((val - minY) * scale + offsetY);
            double rectBottomPdf = docH - (plotY + plotH);

    
            if (showGrid)
            {
                string axisC = isDark ? "0.5 0.5 0.5 RG" : "0 0 0 RG";
                string gridC = isDark ? "0.25 0.25 0.25 RG" : "0.85 0.85 0.85 RG";

                
                cs.AppendLine($"0.5 w {axisC}");
                cs.AppendLine($"{Format(plotX)} {Format(rectBottomPdf)} {Format(plotW)} {Format(plotH)} re S");

                cs.AppendLine(gridC);
                double step = 100.0;

                // X Axis
                double startX = Math.Ceiling(minX / step) * step; if (startX < 200) startX = 200;
                for (double val = startX; val <= maxX; val += step)
                {
                    double px = Tx(val);
                    if (px < plotX - 1 || px > plotX + plotW + 1) continue;

                    cs.AppendLine($"{Format(px)} {Format(rectBottomPdf)} m {Format(px)} {Format(rectBottomPdf + plotH)} l S");

                    // Text X
                    cs.AppendLine("BT /F1 8 Tf 0.5 0.5 0.5 rg");
                    cs.AppendLine($"1 0 0 1 {Format(px - 6)} {Format(rectBottomPdf - 10)} Tm ({val}) Tj ET");
                    cs.AppendLine(gridC);
                }

                // Y Axis
                double startY = Math.Ceiling(minY / step) * step; if (startY < 200) startY = 200;
                for (double val = startY; val <= maxY; val += step)
                {
                    double py = Ty(val);
                    if (py < rectBottomPdf - 1 || py > rectBottomPdf + plotH + 1) continue;

                    cs.AppendLine($"{Format(plotX)} {Format(py)} m {Format(plotX + plotW)} {Format(py)} l S");

                    // Text Y
                    cs.AppendLine("BT /F1 8 Tf 0.5 0.5 0.5 rg");
                    cs.AppendLine($"1 0 0 1 {Format(plotX - 22)} {Format(py - 3)} Tm ({val}) Tj ET");
                    cs.AppendLine(gridC);
                }
            }

            cs.AppendLine($"{Format(plotX)} {Format(rectBottomPdf)} {Format(plotW)} {Format(plotH)} re W n"); // Clip area
            cs.AppendLine("1 J 1 j"); 

            for (int i = 1; i < points.Count; i++)
            {
                var p1 = points[i - 1]; var p2 = points[i];


                double x1 = Tx(p1.X); double y1 = Ty(p1.Y);
                double x2 = Tx(p2.X); double y2 = Ty(p2.Y);

                double dPart = 0;
                if (thickP == DrawingParameter.Pressure) dPart = (p2.P - stats.MinP) / (stats.MaxP - stats.MinP);
                else if (thickP == DrawingParameter.Altitude) dPart = (p2.L - stats.MinL) / (stats.MaxL - stats.MinL);

                double th = (1.5 + dPart * 5.0) * 0.5;
                var rgb = HexToRgb(SvgExporter.GetHexColor(p2, colorP, stats, isDark));

                cs.AppendLine($"{Format(th)} w {rgb.r} {rgb.g} {rgb.b} RG");
                cs.AppendLine($"{Format(x1)} {Format(y1)} m {Format(x2)} {Format(y2)} l S");
            }

            double fY = rectBottomPdf - 25;
            cs.AppendLine("0 0 0 RG BT /F1 10 Tf");
            cs.AppendLine($"1 0 0 1 {Format(squareX + side / 2 - 20)} {Format(fY)} Tm ({EscapePdfString(title)}) Tj ET");

            cs.AppendLine("Q");
        }

        private static string Format(double val) => val.ToString("0.####", CultureInfo.InvariantCulture);

        private static StringBuilder InitPdf(double w, double h, out int contentId, out StringBuilder cs, bool isDark)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%PDF-1.4");
            sb.AppendLine($"1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj");
            sb.AppendLine($"2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj");
            sb.AppendLine($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj");
            sb.AppendLine($"5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj");
            contentId = 4; cs = new StringBuilder();
            string bg = isDark ? "0 0 0" : "1 1 1";
            cs.AppendLine($"{bg} rg 0 0 {w} {h} re f");
            return sb;
        }
        private static void FinalizePdf(StringBuilder sb, StringBuilder cs, int contentId, string path)
        {
            sb.AppendLine($"{contentId} 0 obj\n<< /Length {cs.Length} >>\nstream\n{cs}endstream\nendobj");
            sb.AppendLine("trailer\n<< /Root 1 0 R >>\n%%EOF");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        private static (string r, string g, string b) HexToRgb(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return ("0", "0", "0");
            double r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0;
            double g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0;
            double b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0;
            return (Format(r), Format(g), Format(b));
        }
        private static string EscapePdfString(string text) => text.Replace("(", "\\(").Replace(")", "\\)").Replace("\\", "\\\\");
    }
}