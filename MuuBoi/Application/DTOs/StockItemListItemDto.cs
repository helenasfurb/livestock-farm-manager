namespace MuuBoi.Application.DTOs
{
    public class StockItemListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? UnitAbbreviation { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal StockValue { get; set; }
        public decimal? ReorderPoint { get; set; }
        public EnumValueDto AlertSeverity { get; set; } = null!;
        public bool IsActive { get; set; }
    }
}
