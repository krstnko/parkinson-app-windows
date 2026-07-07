using System.Collections.Generic;
using System.Linq;

namespace ParkinsonAppWindows.Models
{
    public class DrawingStats
    {
        public double MinP { get; set; }
        public double MaxP { get; set; }
        public double MinL { get; set; }
        public double MaxL { get; set; }

        public static DrawingStats Calculate(List<DrawingPoint> points)
        {
            if (points == null || points.Count == 0) return new DrawingStats();
            return new DrawingStats
            {
                MinP = points.Min(p => p.P),
                MaxP = points.Max(p => p.P),
                MinL = points.Min(p => p.L),
                MaxL = points.Max(p => p.L)
            };
        }
    }
}