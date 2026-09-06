using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BreedingEventStatusUpdateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O status é obrigatório.")]
        public ReproductiveEventStatus Status { get; set; }

        [Required(ErrorMessage = "A data do diagnóstico é obrigatória.")]
        public DateTime DiagnosisDate { get; set; }

        [Required(ErrorMessage = "O método de diagnóstico é obrigatório.")]
        public DiagnosisMethod? DiagnosisMethod { get; set; }

        [Range(1, 279, ErrorMessage = "A idade gestacional deve estar entre 1 e 279 dias.")]
        public int? GestationalAge { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Status == ReproductiveEventStatus.AwaitingDiagnosis)
                yield return new ValidationResult(
                    "O status não pode ser alterado para 'Aguardando diagnóstico'.",
                    new[] { nameof(Status) });

            if (DiagnosisDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data do diagnóstico não pode ser futura.",
                    new[] { nameof(DiagnosisDate) });

            if (GestationalAge.HasValue && Status != ReproductiveEventStatus.Successful)
                yield return new ValidationResult(
                    "A idade gestacional só pode ser informada quando a gestação é confirmada.",
                    new[] { nameof(GestationalAge) });
        }
    }
}
