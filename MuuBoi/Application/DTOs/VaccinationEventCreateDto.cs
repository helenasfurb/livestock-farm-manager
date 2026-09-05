using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class VaccinationEventCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "A vacina é obrigatória.")]
        public int VaccineId { get; set; }

        [Required(ErrorMessage = "Informe ao menos um animal.")]
        [MinLength(1, ErrorMessage = "Informe ao menos um animal.")]
        public List<int> AnimalIds { get; set; } = new();

        public DateTime? ApplicationDate { get; set; }

        public DateTime? PredictedDate { get; set; }

        // Optional; defaults to FirstDose (event without a parent) when omitted.
        public DoseType? DoseType { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ApplicationDate.HasValue && !PredictedDate.HasValue)
                yield return new ValidationResult(
                    "Informe ao menos uma data: de aplicação ou prevista.",
                    new[] { nameof(ApplicationDate), nameof(PredictedDate) });

            if (ApplicationDate.HasValue && ApplicationDate.Value.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data de aplicação não pode ser futura.",
                    new[] { nameof(ApplicationDate) });

            if (AnimalIds != null && AnimalIds.Distinct().Count() != AnimalIds.Count)
                yield return new ValidationResult(
                    "A lista de animais não pode conter duplicatas.",
                    new[] { nameof(AnimalIds) });
        }
    }
}
