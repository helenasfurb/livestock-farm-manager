using MuuBoi.Application.Helpers;
using System.Text.Json.Serialization;

namespace MuuBoi.Application.DTOs
{
    public class HealthCaseListItemDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string? AnimalName { get; set; }
        public string? AnimalTagNumber { get; set; }

        public EnumValueDto? DiseaseType { get; set; }
        public string? DiseaseName { get; set; }

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime DiagnosisDate { get; set; }

        public EnumValueDto? Status { get; set; }               // derived

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? MilkLiberationDate { get; set; }       // derived = case liberation

        public IEnumerable<EnumValueDto> AffectedQuarters { get; set; } = new List<EnumValueDto>();
    }
}
