namespace MuuBoi.Application.DTOs
{
    public class StockItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public StockCategoryDto StockCategory { get; set; } = null!;
        public UnitOfMeasureDto UnitOfMeasure { get; set; } = null!;
        public decimal? ReorderPoint { get; set; }
        public int? ReplenishmentLeadDays { get; set; }
        public string? Notes { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal StockValue { get; set; }
        public decimal AverageUnitCost { get; set; }
        public int? DaysOfCoverage { get; set; }
        public DateTime? EstimatedRunOutDate { get; set; }
        public EnumValueDto AlertSeverity { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
