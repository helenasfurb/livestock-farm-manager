using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class SemenSample : BaseEntity, ITenantEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? BullName { get; set; }

        [MaxLength(100)]
        public string? BullRegistration { get; set; }

        [MaxLength(200)]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        public DateTime? CollectedAt { get; set; }

        public DateTime? ManufacturedAt { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }
    }
}
