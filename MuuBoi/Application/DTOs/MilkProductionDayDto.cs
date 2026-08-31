namespace MuuBoi.Application.DTOs
{
    public class MilkProductionDayDto
    {
        public DateTime Date { get; set; }
        public decimal TotalVolume { get; set; }
        public List<MilkProductionListItemDto> Records { get; set; } = new();
    }
}
