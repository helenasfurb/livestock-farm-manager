namespace MuuBoi.Application.DTOs
{
    public class MilkProductionDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public EnumValueDto? Milking { get; set; }
        public decimal Volume { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
