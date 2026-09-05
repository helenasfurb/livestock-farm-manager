namespace MuuBoi.Application.DTOs
{
    public class VaccinationEventDto
    {
        public int Id { get; set; }
        public int VaccineId { get; set; }
        public string? VaccineName { get; set; }
        public EnumValueDto? DoseType { get; set; }
        public DateTime? PredictedDate { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public int? ParentEventId { get; set; }
        public string? Notes { get; set; }
        public List<VaccinationEventAnimalDto> Animals { get; set; } = new();

        // Lineage (D5): the parent event this one descends from (if any) and the booster
        // child spawned from it (if any). Each carries its own dates and derived status.
        public VaccinationEventLineageDto? ParentEvent { get; set; }
        public VaccinationEventLineageDto? ChildEvent { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
