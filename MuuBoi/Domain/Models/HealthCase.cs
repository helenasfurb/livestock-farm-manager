using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    /// <summary>
    /// A single disease occurrence in one animal (the write atom of the curative axis).
    /// Status and milk withdrawal are never stored — resolved at read time.
    /// </summary>
    public class HealthCase : BaseEntity, ITenantEntity
    {
        [Required]
        public int AnimalId { get; set; }

        public DiseaseType DiseaseType { get; set; }

        [MaxLength(100)]
        public string? DiseaseName { get; set; }        // required when Other; null when Mastitis

        public DateTime DiagnosisDate { get; set; }

        public Quarter? AffectedQuarters { get; set; }   // flags; only for mastitis (sparse int? column)

        public DateTime? ResolvedAt { get; set; }        // explicit case closure; firms the withdrawal (null = open)

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
        public ICollection<MastitisTest>? Tests { get; set; }
        public ICollection<AnimalMedication>? Medications { get; set; }
    }
}
