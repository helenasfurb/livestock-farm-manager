using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class StockItem : BaseEntity, ITenantEntity
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

        public StockCategory? StockCategory { get; set; }
        public UnitOfMeasure? UnitOfMeasure { get; set; }
        public ICollection<StockMovement>? Movements { get; set; }
    }
}
