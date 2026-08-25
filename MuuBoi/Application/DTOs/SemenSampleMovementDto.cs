namespace MuuBoi.Application.DTOs
{
    public class SemenSampleMovementDto
    {
        public int Id { get; set; }
        public int SemenSampleId { get; set; }
        public string SemenSampleName { get; set; } = string.Empty;
        public EnumValueDto MovementType { get; set; } = null!;
        public DateTime MovementDate { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
        public int? BreedingEventId { get; set; }
        public bool IsSystemGenerated => BreedingEventId.HasValue;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
