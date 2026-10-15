namespace MuuBoi.Application.DTOs
{
    public class BreedingEventSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalId { get; set; }
        public EnumValueDto? ReproductionType { get; set; }
        public DateTime BreedingDate { get; set; }
        public int? SemenSampleId { get; set; }
        public int? SireAnimalId { get; set; }
        public EnumValueDto? Status { get; set; }
        public DateTime? DiagnosisDate { get; set; }
        public EnumValueDto? DiagnosisMethod { get; set; }
        public int ServiceNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
