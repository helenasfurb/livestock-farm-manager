namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfDto
    {
        public int Id { get; set; }
        public EnumValueDto? Sex { get; set; }
        public decimal? WeightKg { get; set; }
        public EnumValueDto? VitalStatus { get; set; }
        public string? Notes { get; set; }
    }
}
