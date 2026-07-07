using System;
using System.Windows.Media;

namespace ParkinsonAppWindows.Helpers
{
    public static class ColorHelper
    {
        public static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue * 6)) % 6;
            double f = hue * 6 - Math.Floor(hue * 6);

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0) return Color.FromRgb(v, t, p);
            else if (hi == 1) return Color.FromRgb(q, v, p);
            else if (hi == 2) return Color.FromRgb(p, v, t);
            else if (hi == 3) return Color.FromRgb(p, q, v);
            else if (hi == 4) return Color.FromRgb(t, p, v);
            else return Color.FromRgb(v, p, q);
        }

        public static string ColorToHex(Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
