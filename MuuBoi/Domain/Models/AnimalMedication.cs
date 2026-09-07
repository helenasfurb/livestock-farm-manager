using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class AnimalMedication : BaseEntity, ITenantEntity
    {
        [Required]
        public int AnimalId { get; set; }

        public int? MedicationId { get; set; }           // catalog reference (legacy standalone use); null for free-text uses

        [MaxLength(200)]
        public string? MedicationName { get; set; }      // free-text medication name (health case flow — no catalog)

        public int? HealthCaseId { get; set; }          // null = standalone use; set = medication of a health case

        [MaxLength(200)]
        public string? Diagnosis { get; set; }

        public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;  // date of this application (was StartDate)

        public DateTime? EndDate { get; set; }          // legacy course end; not used for milk withdrawal

        [MaxLength(200)]
        public string? DosageDescription { get; set; }

        public int? WithdrawalPeriodDays { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        [MaxLength(500)]
        public string? Observations { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
        public Medication? Medication { get; set; }
        public HealthCase? HealthCase { get; set; }
    }
}
