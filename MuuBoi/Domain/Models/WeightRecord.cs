using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuuBoi.Domain.Models
{
    public class WeightRecord : BaseEntity, ITenantEntity
    {
        [Required]
        [Column(TypeName = "decimal(8,2)")]
        public decimal Weight { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Observations { get; set; }

        [Required]
        public int AnimalId { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
    }
}
