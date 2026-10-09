using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class SemenSample : BaseEntity, ITenantEntity, ISyncable
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BullRegistration { get; set; }

        [MaxLength(200)]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        [MaxLength(100)]
        public string? BatchNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<SemenSampleMovement>? Movements { get; set; }
    }
}
