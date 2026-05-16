using MuuBoi.Converters;
using System.Text.Json.Serialization;

namespace MuuBoi.DTOs
{
    public class AnimalVaccinationDto
    {
        public int Id { get; set; }
        public int VaccineId { get; set; }
        public string VaccineName { get; set; } = string.Empty;

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime ApplicationDate { get; set; }

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? NextApplicationDate { get; set; }

        public string? BatchNumber { get; set; }
        public decimal? DosageMl { get; set; }
        public string? Responsible { get; set; }
        public string? Observations { get; set; }
    }
}
