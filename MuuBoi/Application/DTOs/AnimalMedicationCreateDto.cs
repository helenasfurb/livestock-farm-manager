using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalMedicationCreateDto
    {
        [Required(ErrorMessage = "MedicationId is required")]
        public int? MedicationId { get; set; }

        [MaxLength(200)]
        public string? Diagnosis { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(200)]
        public string? DosageDescription { get; set; }

        public int? WithdrawalPeriodDays { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        [MaxLength(500)]
        public string? Observations { get; set; }
    }
}
