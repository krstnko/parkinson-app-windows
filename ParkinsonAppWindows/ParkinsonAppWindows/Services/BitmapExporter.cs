using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParkinsonAppWindows.Helpers;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.ViewModels;

namespace ParkinsonAppWindows.Services
{
    public static class BitmapExporter
    {
        public static void ExportAll(IEnumerable<ExportViewItem> items, string filePath, string format, bool isDarkMode, bool showGrid)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            // Разрешение 2400px шириной для хорошего качества
            int totalWidth = 2400;
            int cols = 2;
            int rows = (int)Math.Ceiling((double)itemList.Count / cols);
            int cellSide = totalWidth / cols;
            int totalHeight = cellSide * rows;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(isDarkMode ? Brushes.Black : Brushes.White, null, new Rect(0, 0, totalWidth, totalHeight));

                for (int i = 0; i < itemList.Count; i++)
                {
                    int col = i % cols; int row = i / cols;
                    DrawCell(dc, itemList[i], col * cellSide, row * cellSide, cellSide, cellSide, isDarkMode, showGrid);
                }
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            BitmapEncoder encoder;
            if (format == ".jpg" || format == ".jpeg") encoder = new JpegBitmapEncoder();
            else encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Open(filePath, FileMode.Create)) encoder.Save(fs);
        }

        private static void DrawCell(DrawingContext dc, ExportViewItem item, double x, double y, double w, double h, bool isDark, bool showGrid)
        {
            double padding = 40;
            double footerH = 60;

            double availW = w - (padding * 2);
            double availH = h - (padding * 2) - footerH;

            // КВАДРАТ
            double side = Math.Min(availW, availH);
            double squareX = x + padding + (availW - side) / 2;
            double squareY = y + padding + (availH - side) / 2;

            double margin = 60;
            double plotW = side - margin; double plotH = side - margin;
            double plotX = squareX + margin; double plotY = squareY;

            if (plotW <= 0 || plotH <= 0) return;
            Rect plotRect = new Rect(plotX, plotY, plotW, plotH);

            var points = item.Session.DrawingPoints;
            var stats = item.Session.Stats;

            // СЕТКА
            if (showGrid)
            {
                Brush gridBrush = isDark ? new SolidColorBrush(Color.FromRgb(50, 50, 50)) : new SolidColorBrush(Color.FromRgb(200, 200, 200));
                Pen gridPen = new Pen(gridBrush, 2); // Чуть толще для Hi-Res
                Pen axisPen = new Pen(isDark ? Brushes.Gray : Brushes.Black, 3);
                Brush textBrush = isDark ? Brushes.LightGray : Brushes.Gray;
                Typeface tf = new Typeface("Arial");

                dc.DrawRectangle(null, axisPen, plotRect);
                dc.PushClip(new RectangleGeometry(plotRect));

                double minX = points.Min(p => p.X); double maxX = points.Max(p => p.X);
                double minY = points.Min(p => p.Y); double maxY = points.Max(p => p.Y);
                double scale = plotW / Math.Max(Math.Max(maxX - minX, maxY - minY), 1.0);
                double offsetX = plotX + (plotW - (maxX - minX) * scale) / 2;
                double offsetY = plotY + (plotH - (maxY - minY) * scale) / 2;

                double step = 100.0;
                double startX = Math.Ceiling(minX / step) * step; if (startX < 200) startX = 200;
                for (double val = startX; val <= maxX; val += step)
                {
                    double px = (val - minX) * scale + offsetX;
                    dc.DrawLine(gridPen, new Point(px, plotY), new Point(px, plotY + plotH));
                }
                double startY = Math.Ceiling(minY / step) * step; if (startY < 200) startY = 200;
                for (double val = startY; val <= maxY; val += step)
                {
                    double py = (val - minY) * scale + offsetY;
                    dc.DrawLine(gridPen, new Point(plotX, py), new Point(plotX + plotW, py));
                }
                dc.Pop(); // End Clip Grid

                // Текст
                for (double val = startX; val <= maxX; val += step)
                {
                    double px = (val - minX) * scale + offsetX;
                    if (px < plotX || px > plotX + plotW) continue;
                    var txt = new FormattedText(val.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 24, textBrush, 96);
                    dc.DrawText(txt, new Point(px - txt.Width / 2, plotY + plotH + 10));
                }
                for (double val = startY; val <= maxY; val += step)
                {
                    double py = (val - minY) * scale + offsetY;
                    if (py < plotY || py > plotY + plotH) continue;
                    var txt = new FormattedText(val.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 24, textBrush, 96);
                    dc.DrawText(txt, new Point(plotX - txt.Width - 10, py - txt.Height / 2));
                }
            }

            // ГРАФИК
            dc.PushClip(new RectangleGeometry(plotRect));

            double mnX = points.Min(p => p.X); double mxX = points.Max(p => p.X);
            double mnY = points.Min(p => p.Y); double mxY = points.Max(p => p.Y);
            double sc = plotW / Math.Max(Math.Max(mxX - mnX, mxY - mnY), 1.0);
            double offX = plotX + (plotW - (mxX - mnX) * sc) / 2;
            double offY = plotY + (plotH - (mxY - mnY) * sc) / 2;

            for (int i = 1; i < points.Count; i++)
            {
                var p1 = points[i - 1]; var p2 = points[i];

                // УДАЛЕНА ПРОВЕРКА НА 0.001
                // if (Math.Abs(p1.X-p2.X)<0.001) continue; <-- УДАЛЕНО

                Point pt1 = new Point((p1.X - mnX) * sc + offX, (p1.Y - mnY) * sc + offY);
                Point pt2 = new Point((p2.X - mnX) * sc + offX, (p2.Y - mnY) * sc + offY);

                double dPart = 0;
                if (item.ThicknessParam == DrawingParameter.Pressure) dPart = (p2.P - stats.MinP) / (stats.MaxP - stats.MinP);
                else if (item.ThicknessParam == DrawingParameter.Altitude) dPart = (p2.L - stats.MinL) / (stats.MaxL - stats.MinL);

                double th = (3.0 + dPart * 12.0);
                Color c = Colors.Black;
                // ... (Логика цвета прежняя) ...
                if (item.ColorParam == DrawingParameter.Pressure)
                {
                    double hP = 0.33 - Math.Max(0, Math.Min(1, (p2.P - stats.MinP) / (stats.MaxP - stats.MinP))) * 0.33;
                    c = ColorHelper.ColorFromHSV(hP, 1, 0.9);
                }
                else if (item.ColorParam == DrawingParameter.Azimuth)
                {
                    c = ColorHelper.ColorFromHSV((p2.A + Math.PI) / (2 * Math.PI), 1, 1);
                }
                else if (item.ColorParam == DrawingParameter.Altitude)
                {
                    double hL = 0.8 - Math.Max(0, Math.Min(1, (p2.L - stats.MinL) / (stats.MaxL - stats.MinL))) * 0.7;
                    c = ColorHelper.ColorFromHSV(hL, 0.8, 1);
                }
                else { c = isDark ? Colors.White : Colors.Black; }

                Pen pen = new Pen(new SolidColorBrush(c), th);

                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.LineJoin = PenLineJoin.Round;

                dc.DrawLine(pen, pt1, pt2);
            }
            dc.Pop();

            var ft = new FormattedText(item.Session.FileName, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 30, isDark ? Brushes.White : Brushes.Black, 96);
            dc.DrawText(ft, new Point(squareX + side / 2 - ft.Width / 2, squareY + side + 20));
        }
    }
}
