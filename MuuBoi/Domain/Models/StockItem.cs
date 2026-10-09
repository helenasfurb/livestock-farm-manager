using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class StockItem : BaseEntity, ITenantEntity, ISyncable
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int StockCategoryId { get; set; }

        [Required]
        public int UnitOfMeasureId { get; set; }

        public decimal? ReorderPoint { get; set; }

        public int? ReplenishmentLeadDays { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public StockCategory? StockCategory { get; set; }
        public UnitOfMeasure? UnitOfMeasure { get; set; }
        public ICollection<StockMovement>? Movements { get; set; }
    }
}
