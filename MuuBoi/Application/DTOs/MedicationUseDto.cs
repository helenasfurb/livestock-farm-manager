using MuuBoi.Application.Helpers;
using System.Text.Json.Serialization;

namespace MuuBoi.Application.DTOs
{
    /// <summary>A medication application of a health case, with the derived milk liberation date.</summary>
    public class MedicationUseDto
    {
        public int Id { get; set; }
        public string MedicationName { get; set; } = string.Empty;

        [JsonConverter(typeof(DateFormatConverter))]
        public DateTime ApplicationDate { get; set; }

        public int? WithdrawalPeriodDays { get; set; }

        // Derived: ApplicationDate + WithdrawalPeriodDays.
        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? MilkLiberationDate { get; set; }

        public string? Dose { get; set; }
        public string? Responsible { get; set; }
    }
}
