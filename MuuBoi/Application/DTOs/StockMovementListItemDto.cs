namespace MuuBoi.Application.DTOs
{
    public class StockMovementListItemDto
    {
        public int Id { get; set; }
        public string? UnitAbbreviation { get; set; }
        public EnumValueDto MovementType { get; set; } = null!;
        public EnumValueDto MovementReason { get; set; } = null!;
        public DateTime MovementDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal? TotalValue { get; set; }
        public bool IsActive { get; set; }
    }
}
