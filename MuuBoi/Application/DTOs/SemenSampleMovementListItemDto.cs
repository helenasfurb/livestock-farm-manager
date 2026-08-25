namespace MuuBoi.Application.DTOs
{
    public class SemenSampleMovementListItemDto
    {
        public int Id { get; set; }
        public EnumValueDto MovementType { get; set; } = null!;
        public DateTime MovementDate { get; set; }
        public int Quantity { get; set; }
        public int? BreedingEventId { get; set; }
        public bool IsActive { get; set; }
    }
}
