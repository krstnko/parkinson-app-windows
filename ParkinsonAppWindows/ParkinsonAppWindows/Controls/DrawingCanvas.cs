using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.Helpers;

namespace ParkinsonAppWindows.Controls
{
    public class DrawingCanvas : FrameworkElement
    {
        // ... (Все DependencyProperties оставляем как были в прошлом ответе) ...
        public static readonly DependencyProperty FullPointsProperty = DependencyProperty.Register("FullPoints", typeof(List<DrawingPoint>), typeof(DrawingCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty LimitProperty = DependencyProperty.Register("Limit", typeof(int), typeof(DrawingCanvas), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ThicknessParamProperty = DependencyProperty.Register("ThicknessParam", typeof(DrawingParameter), typeof(DrawingCanvas), new FrameworkPropertyMetadata(DrawingParameter.Pressure, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ColorParamProperty = DependencyProperty.Register("ColorParam", typeof(DrawingParameter), typeof(DrawingCanvas), new FrameworkPropertyMetadata(DrawingParameter.Azimuth, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty IsDarkModeProperty = DependencyProperty.Register("IsDarkMode", typeof(bool), typeof(DrawingCanvas), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register("ShowGrid", typeof(bool), typeof(DrawingCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public List<DrawingPoint> FullPoints { get => (List<DrawingPoint>)GetValue(FullPointsProperty); set => SetValue(FullPointsProperty, value); }
        public int Limit { get => (int)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
        public DrawingParameter ThicknessParam { get => (DrawingParameter)GetValue(ThicknessParamProperty); set => SetValue(ThicknessParamProperty, value); }
        public DrawingParameter ColorParam { get => (DrawingParameter)GetValue(ColorParamProperty); set => SetValue(ColorParamProperty, value); }
        public bool IsDarkMode { get => (bool)GetValue(IsDarkModeProperty); set => SetValue(IsDarkModeProperty, value); }
        public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (FullPoints == null || FullPoints.Count < 2 || Limit < 2) return;

            double w = ActualWidth; double h = ActualHeight;
            if (w == 0 || h == 0) return;

            // --- СТРОГАЯ РАМКА ---
            double margin = 40; // Отступ для цифр
            Rect plotRect = new Rect(margin, margin, Math.Max(1, w - 2 * margin), Math.Max(1, h - 2 * margin));

            // Данные
            double minX = FullPoints.Min(p => p.X); double maxX = FullPoints.Max(p => p.X);
            double minY = FullPoints.Min(p => p.Y); double maxY = FullPoints.Max(p => p.Y);
            double dataW = Math.Max(maxX - minX, 1.0); double dataH = Math.Max(maxY - minY, 1.0);

            // Масштаб
            double scale = Math.Min(plotRect.Width / dataW, plotRect.Height / dataH);
            double offsetX = plotRect.Left + (plotRect.Width - dataW * scale) / 2;
            double offsetY = plotRect.Top + (plotRect.Height - dataH * scale) / 2;

            // --- СЕТКА ---
            if (ShowGrid)
            {
                Color gridColor = IsDarkMode ? Color.FromRgb(60, 60, 60) : Color.FromRgb(220, 220, 220);
                Color axisColor = IsDarkMode ? Colors.Gray : Colors.Black;
                Brush textBrush = IsDarkMode ? Brushes.Gray : Brushes.Gray;
                Pen gridPen = new Pen(new SolidColorBrush(gridColor), 1); gridPen.Freeze();
                Pen axisPen = new Pen(new SolidColorBrush(axisColor), 2); axisPen.Freeze(); // Жирная рамка

                dc.DrawRectangle(null, axisPen, plotRect);

                dc.PushClip(new RectangleGeometry(plotRect)); // Обрезаем линии
                double step = 100.0;

                double startX = Math.Ceiling(minX / step) * step; if (startX < 200) startX = 200;
                for (double val = startX; val <= maxX; val += step)
                {
                    double xPos = (val - minX) * scale + offsetX;
                    dc.DrawLine(gridPen, new Point(xPos, plotRect.Top), new Point(xPos, plotRect.Bottom));
                }

                double startY = Math.Ceiling(minY / step) * step; if (startY < 200) startY = 200;
                for (double val = startY; val <= maxY; val += step)
                {
                    double yPos = (val - minY) * scale + offsetY;
                    dc.DrawLine(gridPen, new Point(plotRect.Left, yPos), new Point(plotRect.Right, yPos));
                }
                dc.Pop(); // Снимаем обрезку для текста

                // Текст (снаружи рамки)
                Typeface tf = new Typeface("Arial");
                for (double val = startX; val <= maxX; val += step)
                {
                    double xPos = (val - minX) * scale + offsetX;
                    if (xPos < plotRect.Left || xPos > plotRect.Right) continue;
                    var txt = new FormattedText(val.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 10, textBrush, 96);
                    dc.DrawText(txt, new Point(xPos - txt.Width / 2, plotRect.Bottom + 5));
                }
                for (double val = startY; val <= maxY; val += step)
                {
                    double yPos = (val - minY) * scale + offsetY;
                    if (yPos < plotRect.Top || yPos > plotRect.Bottom) continue;
                    var txt = new FormattedText(val.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 10, textBrush, 96);
                    dc.DrawText(txt, new Point(plotRect.Left - txt.Width - 5, yPos - txt.Height / 2));
                }
            }

            // --- ГРАФИК ---
            dc.PushClip(new RectangleGeometry(plotRect)); // Обрезаем график

            var stats = DrawingStats.Calculate(FullPoints);
            int count = Math.Min(Limit, FullPoints.Count);

            // Визуальный масштаб (для толщины)
            double visualScale = Math.Max(0.1, scale / (800.0 / dataW));

            for (int i = 1; i < count; i++)
            {
                var p1 = FullPoints[i - 1]; var p2 = FullPoints[i];
                if (Math.Abs(p1.X - p2.X) < 0.001 && Math.Abs(p1.Y - p2.Y) < 0.001) continue;

                Point pt1 = new Point((p1.X - minX) * scale + offsetX, (p1.Y - minY) * scale + offsetY);
                Point pt2 = new Point((p2.X - minX) * scale + offsetX, (p2.Y - minY) * scale + offsetY);

                double dPart = 0;
                switch (ThicknessParam)
                {
                    case DrawingParameter.Pressure: dPart = Norm(p2.P, stats.MinP, stats.MaxP); break;
                    case DrawingParameter.Altitude: dPart = Norm(p2.L, stats.MinL, stats.MaxL); break;
                }
                double thick = (2.0 + dPart * 10.0) * visualScale;

                Color c = Colors.Black;
                switch (ColorParam)
                {
                    case DrawingParameter.Pressure: c = ColorHelper.ColorFromHSV(0.33 - Norm(p2.P, stats.MinP, stats.MaxP) * 0.33, 1, 0.9); break;
                    case DrawingParameter.Azimuth: c = ColorHelper.ColorFromHSV((p2.A + Math.PI) / (2 * Math.PI), 1, 1); break;
                    case DrawingParameter.Altitude: c = ColorHelper.ColorFromHSV(0.8 - Norm(p2.L, stats.MinL, stats.MaxL) * 0.7, 0.8, 1); break;
                    default: c = IsDarkMode ? Colors.White : Colors.Black; break;
                }

                var pen = new Pen(new SolidColorBrush(c), thick);
                pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round; pen.Freeze();
                dc.DrawLine(pen, pt1, pt2);
            }
            dc.Pop();
        }
        private double Norm(double v, double min, double max) => max <= min ? 0 : Math.Max(0, Math.Min(1, (v - min) / (max - min)));
    }
}