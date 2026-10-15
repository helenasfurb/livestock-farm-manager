namespace MuuBoi.Application.DTOs
{
    public class AnimalPregnancySyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalId { get; set; }
        public int? BreedingEventId { get; set; }
        public int? SireAnimalId { get; set; }
        public int? SemenSampleId { get; set; }
        public DateTime ConfirmationDate { get; set; }
        public DateTime ExpectedCalvingDate { get; set; }
        public int? GestationalAge { get; set; }
        public DateTime? LossDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
