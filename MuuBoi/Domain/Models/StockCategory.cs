using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class StockCategory : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<StockItem>? Items { get; set; }
    }
}
