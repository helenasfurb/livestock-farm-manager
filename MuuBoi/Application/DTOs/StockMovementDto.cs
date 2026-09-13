namespace MuuBoi.Application.DTOs
{
    public class StockMovementDto
    {
        public int Id { get; set; }
        public int StockItemId { get; set; }
        public string StockItemName { get; set; } = string.Empty;
        public string? UnitAbbreviation { get; set; }
        public EnumValueDto MovementType { get; set; } = null!;
        public EnumValueDto MovementReason { get; set; } = null!;
        public DateTime MovementDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal? TotalValue { get; set; }
        public decimal? UnitCost { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
