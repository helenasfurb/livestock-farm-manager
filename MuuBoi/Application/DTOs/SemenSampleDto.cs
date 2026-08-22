namespace MuuBoi.Application.DTOs
{
    public class SemenSampleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BullName { get; set; }
        public string? BullRegistration { get; set; }
        public string? GeneticsCompany { get; set; }
        public EnumValueDto? BullBreed { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ManufacturedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
