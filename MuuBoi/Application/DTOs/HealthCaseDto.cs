using MuuBoi.Application.Helpers;
using System.Text.Json.Serialization;

namespace MuuBoi.Application.DTOs
{
    /// <summary>Health case detail, with derived status/liberation and nested medications and tests.</summary>
    public class HealthCaseDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string? AnimalName { get; set; }
        public string? AnimalTagNumber { get; set; }

        public EnumValueDto? DiseaseType { get; set; }
        public string? DiseaseName { get; set; }

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime DiagnosisDate { get; set; }

        // Affected quarters decomposed from the flags value (empty for non-mastitis).
        public IEnumerable<EnumValueDto> AffectedQuarters { get; set; } = new List<EnumValueDto>();

        public EnumValueDto? Status { get; set; }               // derived

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? CaseLiberationDate { get; set; }       // derived = MAX(medication liberation)

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? ResolvedAt { get; set; }

        public string? Notes { get; set; }

        public IEnumerable<MedicationUseDto> Medications { get; set; } = new List<MedicationUseDto>();
        public IEnumerable<MastitisTestDto> Tests { get; set; } = new List<MastitisTestDto>();

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
