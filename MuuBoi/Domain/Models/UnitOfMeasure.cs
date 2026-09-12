using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class UnitOfMeasure : BaseEntity
    {
        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Abbreviation { get; set; }

        public ICollection<StockItem>? Items { get; set; }
    }
}
