namespace MuuBoi.Application.DTOs
{
    /// <summary>A single line in an animal's vaccination history (applied events only).</summary>
    public class VaccinationHistoryItemDto
    {
        public int VaccinationEventId { get; set; }
        public int VaccineId { get; set; }
        public string? VaccineName { get; set; }
        public DateTime ApplicationDate { get; set; }
        public EnumValueDto? DoseType { get; set; }

        // Predicted date of the next dose (the booster child spawned from this event), if any.
        public DateTime? NextDoseDate { get; set; }
    }
}
