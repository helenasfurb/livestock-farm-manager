using MuuBoi.Application.Helpers;
using System.Text.Json.Serialization;

namespace MuuBoi.Application.DTOs
{
    public class MastitisTestDto
    {
        public int Id { get; set; }
        public EnumValueDto? TestType { get; set; }
        public string Result { get; set; } = string.Empty;

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime TestDate { get; set; }
    }
}
