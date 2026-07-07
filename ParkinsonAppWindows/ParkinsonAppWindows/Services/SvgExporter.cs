using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using ParkinsonAppWindows.Helpers;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.ViewModels;

namespace ParkinsonAppWindows.Services
{
    public static class SvgExporter
    {
        public static void ExportToSvg(List<DrawingPoint> points, DrawingStats stats, string filePath, DrawingParameter thicknessParam, DrawingParameter colorParam, bool isDarkMode, bool showGrid, string fileNameOnly)
        {
            if (points == null || points.Count < 2) return;

            double width = 1024.0;
            double height = 1024.0; 

            var sb = new StringBuilder();
            sb.AppendLine($"<svg width=\"{(int)width}\" height=\"{(int)height}\" viewBox=\"0 0 {(int)width} {(int)height}\" xmlns=\"http://www.w3.org/2000/svg\">");

            string bg = isDarkMode ? "black" : "white";
            sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{bg}\"/>");

            DrawGraphLogic(sb, points, stats, 0, 0, width, height, thicknessParam, colorParam, isDarkMode, showGrid, fileNameOnly, true);

            sb.AppendLine("</svg>");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportAllToSvg(IEnumerable<ExportViewItem> items, string filePath, bool isDarkMode, bool showGrid)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            double totalWidth = 1200.0;
           
            double totalHeight = 1600.0; 

            int count = itemList.Count;
            int cols = 2; 
            int rows = (int)Math.Ceiling((double)count / cols);

            double cellSide = totalWidth / cols;
            totalHeight = cellSide * rows;

            var sb = new StringBuilder();
            sb.AppendLine($"<svg width=\"{(int)totalWidth}\" height=\"{(int)totalHeight}\" viewBox=\"0 0 {(int)totalWidth} {(int)totalHeight}\" xmlns=\"http://www.w3.org/2000/svg\">");

            string bg = isDarkMode ? "black" : "white";
            sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{bg}\"/>");

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                double cellX = col * cellSide;
                double cellY = row * cellSide;

                var item = itemList[i];
                DrawGraphLogic(sb, item.Session.DrawingPoints, item.Session.Stats, cellX, cellY, cellSide, cellSide, item.ThicknessParam, item.ColorParam, isDarkMode, showGrid, item.Session.FileName, false);
            }

            sb.AppendLine("</svg>");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void DrawGraphLogic(StringBuilder sb, List<DrawingPoint> points, DrawingStats stats, double x, double y, double w, double h, DrawingParameter thickParam, DrawingParameter colorParam, bool isDarkMode, bool showGrid, string title, bool isSingleMode)
        {
            double padding = isSingleMode ? 40.0 : 20.0;
            double footerH = 40.0; 

            
            double availW = w - (padding * 2);
            double availH = h - (padding * 2) - footerH;

            
            double side = Math.Min(availW, availH);

            double squareX = x + padding + (availW - side) / 2;
            double squareY = y + padding + (availH - side) / 2;

            double margin = 40.0; 

            double plotW = side - margin; 
            double plotH = side - margin; 

            
            double plotX = squareX + margin;
            double plotY = squareY;

            if (plotW <= 0 || plotH <= 0) return;

            double minX = points.Min(p => p.X); double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y); double maxY = points.Max(p => p.Y);

            double dataMax = Math.Max(maxX - minX, maxY - minY);
            double scale = plotW / Math.Max(dataMax, 1.0);

            double contentW = (maxX - minX) * scale;
            double contentH = (maxY - minY) * scale;

            double offsetX = plotX + (plotW - contentW) / 2;
            double offsetY = plotY + (plotH - contentH) / 2;

            string gridColor = isDarkMode ? "#333333" : "#DDDDDD";
            string axisColor = isDarkMode ? "#888888" : "black";
            string textColor = isDarkMode ? "#AAAAAA" : "#555555";
            double step = 100.0;

            if (showGrid)
            {
                sb.AppendLine($"<rect x=\"{plotX}\" y=\"{plotY}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"none\" stroke=\"{axisColor}\" stroke-width=\"2\"/>");

                var culture = CultureInfo.InvariantCulture;

                double startX = Math.Ceiling(minX / step) * step; if (startX < 200) startX = 200;
                for (double val = startX; val <= maxX; val += step)
                {
                    double px = (val - minX) * scale + offsetX;
                    if (px < plotX || px > plotX + plotW) continue;

                    sb.AppendLine($"<line x1=\"{px}\" y1=\"{plotY}\" x2=\"{px}\" y2=\"{plotY + plotH}\" stroke=\"{gridColor}\" stroke-width=\"1\"/>");
                    sb.AppendLine($"<text x=\"{px}\" y=\"{plotY + plotH + 20}\" font-family=\"Arial\" font-size=\"12\" fill=\"{textColor}\" text-anchor=\"middle\">{val}</text>");
                }

                double startY = Math.Ceiling(minY / step) * step; if (startY < 200) startY = 200;
                for (double val = startY; val <= maxY; val += step)
                {
                    double py = (val - minY) * scale + offsetY;
                    if (py < plotY || py > plotY + plotH) continue;

                    sb.AppendLine($"<line x1=\"{plotX}\" y1=\"{py}\" x2=\"{plotX + plotW}\" y2=\"{py}\" stroke=\"{gridColor}\" stroke-width=\"1\"/>");
                    sb.AppendLine($"<text x=\"{plotX - 10}\" y=\"{py + 4}\" font-family=\"Arial\" font-size=\"12\" fill=\"{textColor}\" text-anchor=\"end\">{val}</text>");
                }
            }

            string clipId = $"clip_{Guid.NewGuid()}";
            sb.AppendLine($"<clipPath id=\"{clipId}\"><rect x=\"{plotX}\" y=\"{plotY}\" width=\"{plotW}\" height=\"{plotH}\"/></clipPath>");
            sb.AppendLine($"<g clip-path=\"url(#{clipId})\">");

            for (int i = 1; i < points.Count; i++)
            {
                var p1 = points[i - 1]; var p2 = points[i];
                if (Math.Abs(p1.X - p2.X) < 0.001 && Math.Abs(p1.Y - p2.Y) < 0.001) continue;

                double x1 = (p1.X - minX) * scale + offsetX;
                double y1 = (p1.Y - minY) * scale + offsetY;
                double x2 = (p2.X - minX) * scale + offsetX;
                double y2 = (p2.Y - minY) * scale + offsetY;

                double dPart = 0;
                if (thickParam == DrawingParameter.Pressure) dPart = (p2.P - stats.MinP) / (stats.MaxP - stats.MinP);
                else if (thickParam == DrawingParameter.Altitude) dPart = (p2.L - stats.MinL) / (stats.MaxL - stats.MinL);

                double wStr = (2.0 + dPart * 8.0) * 0.5;
                string cHex = GetHexColor(p2, colorParam, stats, isDarkMode);

                sb.AppendLine($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{cHex}\" stroke-width=\"{wStr}\" stroke-linecap=\"round\" />");
            }
            sb.AppendLine("</g>");

            double txtY = squareY + side + 25;
            string label = isSingleMode ? $"{title}  (Thickness: {thickParam}, Color: {colorParam})" : title;
            sb.AppendLine($"<text x=\"{squareX + side / 2}\" y=\"{txtY}\" font-family=\"Arial\" font-size=\"14\" fill=\"{textColor}\" text-anchor=\"middle\">{label}</text>");
        }

        public static string GetHexColor(DrawingPoint p, DrawingParameter param, DrawingStats stats, bool isDarkMode)
        {
            Color c = Colors.Black;
            double Norm(double v, double min, double max) => max <= min ? 0 : Math.Max(0, Math.Min(1, (v - min) / (max - min)));

            switch (param)
            {
                case DrawingParameter.Pressure: c = ColorHelper.ColorFromHSV(0.33 - Norm(p.P, stats.MinP, stats.MaxP) * 0.33, 1.0, 0.9); break;
                case DrawingParameter.Azimuth: c = ColorHelper.ColorFromHSV((p.A + Math.PI) / (2 * Math.PI), 1.0, 1.0); break;
                case DrawingParameter.Altitude: c = ColorHelper.ColorFromHSV(0.8 - Norm(p.L, stats.MinL, stats.MaxL) * 0.7, 0.8, 1.0); break;
                default: c = isDarkMode ? Colors.White : Colors.Black; break;
            }
            return ColorHelper.ColorToHex(c);
        }
    }
}