using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class AnimalCalvingCalf : BaseEntity, ITenantEntity, ISyncable
    {
        public int CalvingId { get; set; }

        public int? AnimalId { get; set; }

        public AnimalGender Sex { get; set; }

        [Range(0.01, 999.99)]
        public decimal? WeightKg { get; set; }

        public CalfVitalStatus VitalStatus { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AnimalCalving? Calving { get; set; }
        public Animal? Animal { get; set; }
    }
}
