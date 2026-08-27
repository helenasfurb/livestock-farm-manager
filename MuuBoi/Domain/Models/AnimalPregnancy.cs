using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class AnimalPregnancy : BaseEntity, ITenantEntity
    {
        public int AnimalId { get; set; }

        public int BreedingEventId { get; set; }

        public DateTime ConfirmationDate { get; set; }

        public DateTime ExpectedCalvingDate { get; set; }

        public DateTime? LossDate { get; set; }

        public AnimalPregnancyStatus Status { get; set; } = AnimalPregnancyStatus.Confirmed;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
        public BreedingEvent? BreedingEvent { get; set; }
        public ICollection<AnimalCalving>? Calvings { get; set; }
    }
}
