namespace MuuBoi.Application.DTOs
{
    public class SemenSampleListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? GeneticsCompany { get; set; }
        public EnumValueDto? BullBreed { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? BatchDate { get; set; }
        public int AvailableDoses { get; set; }
        public bool IsActive { get; set; }
    }
}
