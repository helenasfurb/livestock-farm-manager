using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class BodyConditionRecord : BaseEntity, ITenantEntity, ISyncable
    {
        public int AnimalId { get; set; }

        public BodyConditionScore Score { get; set; }

        public DateTime RecordedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Animal? Animal { get; set; }
    }
}
