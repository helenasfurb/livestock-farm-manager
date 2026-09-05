namespace MuuBoi.Application.DTOs
{
    public class AnimalPregnancyDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string AnimalTagNumber { get; set; } = string.Empty;
        public int? BreedingEventId { get; set; }
        public int? SireAnimalId { get; set; }
        public string? SireAnimalTagNumber { get; set; }
        public int? SemenSampleId { get; set; }
        public string? SemenSampleName { get; set; }
        public DateTime ConfirmationDate { get; set; }
        public DateTime ExpectedCalvingDate { get; set; }
        public DateTime? LossDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public AnimalCalvingDto? Calving { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
