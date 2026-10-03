using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class MilkProduction : BaseEntity, ITenantEntity, ISyncable
    {
        public DateTime Date { get; set; }

        public MilkingShift? Milking { get; set; }        // null = não especificado / total do dia

        [Range(0.01, 9999999.99)]
        public decimal Volume { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
