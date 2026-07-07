using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ParkinsonAppWindows.Models
{
    public class DrawingDataFile
    {
        [JsonPropertyName("data")]
        public List<List<DrawingPoint>> Data { get; set; }

        [JsonIgnore]
        public string FileName { get; set; }
    }
}