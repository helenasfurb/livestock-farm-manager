using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class AnimalMedication : BaseEntity
    {
        [Required]
        public int AnimalId { get; set; }

        [Required]
        public int MedicationId { get; set; }

        [MaxLength(200)]
        public string? Diagnosis { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        [MaxLength(200)]
        public string? DosageDescription { get; set; }

        public int? WithdrawalPeriodDays { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        [MaxLength(500)]
        public string? Observations { get; set; }

        public Animal? Animal { get; set; }
        public Medication? Medication { get; set; }
    }
}
