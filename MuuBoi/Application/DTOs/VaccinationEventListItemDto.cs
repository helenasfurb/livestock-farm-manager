namespace MuuBoi.Application.DTOs
{
    public class VaccinationEventListItemDto
    {
        public int Id { get; set; }
        public int VaccineId { get; set; }
        public string? VaccineName { get; set; }
        public EnumValueDto? DoseType { get; set; }
        public DateTime? PredictedDate { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public string? Notes { get; set; }
        public int AnimalCount { get; set; }
    }
}
