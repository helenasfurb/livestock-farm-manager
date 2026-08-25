namespace MuuBoi.Application.DTOs
{
    public class SemenSampleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BullRegistration { get; set; }
        public string? GeneticsCompany { get; set; }
        public EnumValueDto? BullBreed { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? BatchDate { get; set; }
        public string? Notes { get; set; }
        public int AvailableDoses { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
