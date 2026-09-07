using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class HealthCaseCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O animal é obrigatório.")]
        public int AnimalId { get; set; }

        public DiseaseType DiseaseType { get; set; }

        [MaxLength(100)]
        public string? DiseaseName { get; set; }

        [Required(ErrorMessage = "A data de diagnóstico é obrigatória.")]
        public DateTime DiagnosisDate { get; set; }

        public Quarter? AffectedQuarters { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(typeof(DiseaseType), DiseaseType))
                yield return new ValidationResult(
                    "Informe o tipo de doença.",
                    new[] { nameof(DiseaseType) });

            if (DiseaseType == DiseaseType.Other && string.IsNullOrWhiteSpace(DiseaseName))
                yield return new ValidationResult(
                    "Informe o nome da doença quando o tipo for 'Outra'.",
                    new[] { nameof(DiseaseName) });

            if (DiseaseType != DiseaseType.Mastitis && AffectedQuarters.HasValue)
                yield return new ValidationResult(
                    "Quartos afetados só se aplicam a casos de mastite.",
                    new[] { nameof(AffectedQuarters) });

            if (DiagnosisDate.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de diagnóstico não pode ser futura.",
                    new[] { nameof(DiagnosisDate) });
        }
    }
}
