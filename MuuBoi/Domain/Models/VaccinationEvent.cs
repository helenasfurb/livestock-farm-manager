using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    /// <summary>
    /// The single write atom of the vaccination flow. A batch event (many animals via
    /// <see cref="VaccinationEventAnimal"/>). Status is derived at read time, never stored.
    /// Invariant: at least one of <see cref="PredictedDate"/> / <see cref="ApplicationDate"/> is set.
    /// </summary>
    public class VaccinationEvent : BaseEntity, ITenantEntity
    {
        [Required]
        public int VaccineId { get; set; }

        // Default comes from the lineage, editable (D6).
        public DoseType DoseType { get; set; }

        // Agenda date; set on spawn (D3). Feeds the Scheduled/Overdue status.
        public DateTime? PredictedDate { get; set; }

        // Real application date; sovereign in the history (D3). Must be <= today.
        public DateTime? ApplicationDate { get; set; }

        // Parent -> child lineage; guard of a single child per parent (D5).
        public int? ParentEventId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public Vaccine? Vaccine { get; set; }
        public VaccinationEvent? ParentEvent { get; set; }
        public ICollection<VaccinationEventAnimal>? EventAnimals { get; set; }
    }
}
