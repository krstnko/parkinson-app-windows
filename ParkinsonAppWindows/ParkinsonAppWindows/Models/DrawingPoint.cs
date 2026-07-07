using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ParkinsonAppWindows.Models
{
    public class DrawingPoint
    {
        [JsonPropertyName("a")] public double A { get; set; } // Azimuth
        [JsonPropertyName("t")] public double T { get; set; } // Timestamp
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("l")] public double L { get; set; } // Altitude
        [JsonPropertyName("p")] public double P { get; set; } // Pressure
    }

}
