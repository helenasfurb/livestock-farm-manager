using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class StockMovementFilterDto
    {
        public StockMovementType? MovementType { get; set; }
        public StockMovementReason? MovementReason { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
