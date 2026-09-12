namespace MuuBoi.Application.DTOs
{
    public class StockDashboardFilterDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int? StockCategoryId { get; set; }
        public int? StockItemId { get; set; }
    }
}
