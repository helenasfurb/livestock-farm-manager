using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Partial update (PATCH): effectuate an event (set ApplicationDate) or fix its dates/dose.
    /// All fields optional; null = do not change. Creating a booster is a separate action
    /// (POST /vaccination-events/{id}/booster).
    /// </summary>
    public class VaccinationEventUpdateDto : IValidatableObject
    {
        public DateTime? ApplicationDate { get; set; }

        public DateTime? PredictedDate { get; set; }

        public DoseType? DoseType { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ApplicationDate.HasValue && ApplicationDate.Value.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de aplicação não pode ser futura.",
                    new[] { nameof(ApplicationDate) });
        }
    }
}
