namespace MuuBoi.Application.DTOs
{
    public class StockAlertDto
    {
        public int StockItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? UnitAbbreviation { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal? ReorderPoint { get; set; }
        public DateTime? EstimatedRunOutDate { get; set; }
        public EnumValueDto Severity { get; set; } = null!;
    }
}
