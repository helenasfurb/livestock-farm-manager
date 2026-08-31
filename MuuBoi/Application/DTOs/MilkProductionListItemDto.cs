namespace MuuBoi.Application.DTOs
{
    public class MilkProductionListItemDto
    {
        public int Id { get; set; }
        public EnumValueDto? Milking { get; set; }
        public decimal Volume { get; set; }
        public string? Notes { get; set; }
    }
}
