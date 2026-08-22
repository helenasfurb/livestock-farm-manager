namespace MuuBoi.Application.DTOs
{
    public class SemenSampleListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BullName { get; set; }
        public string? GeneticsCompany { get; set; }
        public EnumValueDto? BullBreed { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }
}
