namespace MuuBoi.Application.DTOs
{
    public class StockDashboardDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal PeriodSpent { get; set; }
        public decimal PeriodConsumedValue { get; set; }
        public decimal StockValue { get; set; }
        public List<StockDashboardItemDto> Items { get; set; } = new();
    }

    public class StockDashboardItemDto
    {
        public int StockItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? UnitAbbreviation { get; set; }
        public decimal ConsumedQuantity { get; set; }
        public decimal CurrentBalance { get; set; }
    }
}
