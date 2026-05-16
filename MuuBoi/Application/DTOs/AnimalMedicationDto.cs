using MuuBoi.Converters;
using System.Text.Json.Serialization;

namespace MuuBoi.DTOs
{
    public class AnimalMedicationDto
    {
        public int Id { get; set; }
        public int MedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string? Diagnosis { get; set; }

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime StartDate { get; set; }

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? EndDate { get; set; }

        public string? DosageDescription { get; set; }
        public int? WithdrawalPeriodDays { get; set; }
        public string? Responsible { get; set; }
        public string? Observations { get; set; }
    }
}
