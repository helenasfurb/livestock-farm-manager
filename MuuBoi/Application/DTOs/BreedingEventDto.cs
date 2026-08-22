namespace MuuBoi.Application.DTOs
{
    public class BreedingEventDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string AnimalTagNumber { get; set; } = string.Empty;
        public EnumValueDto? ReproductionType { get; set; }
        public DateTime BreedingDate { get; set; }
        public int? SemenSampleId { get; set; }
        public string? SemenSampleName { get; set; }
        public int? SireAnimalId { get; set; }
        public string? SireAnimalTagNumber { get; set; }
        public string? SireAnimalName { get; set; }
        public EnumValueDto? Status { get; set; }
        public DateTime? DiagnosisDate { get; set; }
        public int ServiceNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
