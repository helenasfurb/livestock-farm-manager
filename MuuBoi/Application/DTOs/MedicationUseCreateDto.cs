using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    /// <summary>Append a medication application to a health case (POST /health-cases/{id}/medications).</summary>
    public class MedicationUseCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O medicamento é obrigatório.")]
        [MaxLength(200)]
        public string MedicationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de aplicação é obrigatória.")]
        public DateTime ApplicationDate { get; set; }

        // Milk withdrawal in days; defaults to the catalog's DefaultWithdrawalPeriodDays when omitted.
        public int? WithdrawalPeriodDays { get; set; }

        [MaxLength(200)]
        public string? Dose { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ApplicationDate.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de aplicação não pode ser futura.",
                    new[] { nameof(ApplicationDate) });

            if (WithdrawalPeriodDays.HasValue && WithdrawalPeriodDays.Value < 0)
                yield return new ValidationResult(
                    "A carência não pode ser negativa.",
                    new[] { nameof(WithdrawalPeriodDays) });
        }
    }
}
