using System;
using System.Collections.Generic;
using System.Linq;

namespace ParkinsonAppWindows.Models
{
    public enum DrawingParameter
    {
        None,
        Pressure, // Давление (p)
        Azimuth,  // Азимут (a)
        Altitude  // Высота (l)
    }

    public class DrawingStats
    {
        public double MinP { get; set; } = 0; public double MaxP { get; set; } = 1;
        public double MinA { get; set; } = 0; public double MaxA { get; set; } = 1;
        public double MinL { get; set; } = 0; public double MaxL { get; set; } = 1;

        public static DrawingStats Calculate(List<DrawingPoint> points)
        {
            if (points == null || points.Count == 0) return new DrawingStats();

            return new DrawingStats
            {
                MinP = points.Min(p => p.P),
                MaxP = points.Max(p => p.P),
                MinA = points.Min(p => p.A),
                MaxA = points.Max(p => p.A),
                MinL = points.Min(p => p.L),
                MaxL = points.Max(p => p.L)
            };
        }
    }
}
