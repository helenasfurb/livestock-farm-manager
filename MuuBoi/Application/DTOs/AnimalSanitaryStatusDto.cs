namespace MuuBoi.Application.DTOs
{
    /// <summary>Resolved sanitary status of an animal, for composition into animal read models.</summary>
    public class AnimalSanitaryStatusDto
    {
        public EnumValueDto? Status { get; set; }
        public DateTime? MilkWithheldUntil { get; set; }
    }
}
