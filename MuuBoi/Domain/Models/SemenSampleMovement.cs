using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class SemenSampleMovement : BaseEntity, ITenantEntity
    {
        public int SemenSampleId { get; set; }

        public SemenMovementType MovementType { get; set; }

        public DateTime MovementDate { get; set; }

        [Range(1, 9999, ErrorMessage = "A quantidade deve ser entre 1 e 9.999.")]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public int? BreedingEventId { get; set; }

        public Guid PropertyId { get; set; }

        public SemenSample? SemenSample { get; set; }
        public BreedingEvent? BreedingEvent { get; set; }
    }
}
