using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuuBoi.Domain.Models
{
    public class WeightRecord : BaseEntity, ITenantEntity, ISyncable
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

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Animal? Animal { get; set; }
    }
}
