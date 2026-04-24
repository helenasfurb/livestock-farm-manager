using MuuBoi.Converters;
using System.Text.Json.Serialization;

namespace MuuBoi.DTOs
{
    public class WeightRecordDto
    {
        public int Id { get; set; }
        public decimal Weight { get; set; }

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime RecordedAt { get; set; }

        public string? Observations { get; set; }
    }
}
