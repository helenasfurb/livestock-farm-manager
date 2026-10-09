using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class StockMovement : BaseEntity, ITenantEntity, ISyncable
    {
        [Required]
        public int StockItemId { get; set; }

        public StockMovementType MovementType { get; set; }

        public StockMovementReason MovementReason { get; set; }

        public DateTime MovementDate { get; set; }

        [Range(0.001, 9999999.999, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal Quantity { get; set; }

        public decimal? TotalValue { get; set; }

        public ValueEntryMode? ValueEntryMode { get; set; }

        public decimal? UnitCostSnapshot { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public StockItem? StockItem { get; set; }
    }
}
