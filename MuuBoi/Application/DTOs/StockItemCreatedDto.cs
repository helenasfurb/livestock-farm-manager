namespace MuuBoi.Application.DTOs
{
    public class StockItemCreatedDto : StockItemDto
    {
        public StockMovementRefDto? InitialMovement { get; set; }
    }
}
