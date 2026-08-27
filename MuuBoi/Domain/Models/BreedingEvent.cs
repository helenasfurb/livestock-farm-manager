using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class BreedingEvent : BaseEntity, ITenantEntity
    {
        public int AnimalId { get; set; }

        public ReproductionType ReproductionType { get; set; }

        public DateTime BreedingDate { get; set; }

        public int? SemenSampleId { get; set; }

        public int? SireAnimalId { get; set; }

        public ReproductiveEventStatus Status { get; set; } = ReproductiveEventStatus.AwaitingDiagnosis;

        public DateTime? DiagnosisDate { get; set; }

        public int ServiceNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
        public SemenSample? SemenSample { get; set; }
        public Animal? SireAnimal { get; set; }
        public AnimalPregnancy? Pregnancy { get; set; }
    }
}
