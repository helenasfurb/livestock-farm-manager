using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    /// <summary>Partial update (PATCH). Null fields are left unchanged.</summary>
    public class HealthCaseUpdateDto : IValidatableObject
    {
        [MaxLength(100)]
        public string? DiseaseName { get; set; }

        public DateTime? DiagnosisDate { get; set; }

        public Quarter? AffectedQuarters { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DiagnosisDate.HasValue && DiagnosisDate.Value.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de diagnóstico não pode ser futura.",
                    new[] { nameof(DiagnosisDate) });

            if (ResolvedAt.HasValue && ResolvedAt.Value.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de encerramento não pode ser futura.",
                    new[] { nameof(ResolvedAt) });
        }
    }
}
