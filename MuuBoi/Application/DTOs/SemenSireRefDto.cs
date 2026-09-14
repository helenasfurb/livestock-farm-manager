namespace MuuBoi.Application.DTOs
{
    public class SemenSireRefDto
    {
        public int SemenSampleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BullRegistration { get; set; }
        public EnumValueDto? BullBreed { get; set; }
        public string? GeneticsCompany { get; set; }
    }
}
