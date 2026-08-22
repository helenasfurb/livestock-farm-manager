using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BreedingEventCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O tipo de reprodução é obrigatório.")]
        public ReproductionType ReproductionType { get; set; }

        [Required(ErrorMessage = "A data da cobertura é obrigatória.")]
        public DateTime BreedingDate { get; set; }

        public int? SemenSampleId { get; set; }

        public int? SireAnimalId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (BreedingDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data da cobertura não pode ser futura.",
                    new[] { nameof(BreedingDate) });

            if (ReproductionType == ReproductionType.ArtificialInsemination)
            {
                if (!SemenSampleId.HasValue)
                    yield return new ValidationResult(
                        "O sêmen é obrigatório para inseminação artificial.",
                        new[] { nameof(SemenSampleId) });

                if (SireAnimalId.HasValue)
                    yield return new ValidationResult(
                        "O touro pai não deve ser informado para inseminação artificial.",
                        new[] { nameof(SireAnimalId) });
            }

            if (ReproductionType == ReproductionType.NaturalMating)
            {
                if (!SireAnimalId.HasValue)
                    yield return new ValidationResult(
                        "O touro pai é obrigatório para monta natural.",
                        new[] { nameof(SireAnimalId) });

                if (SemenSampleId.HasValue)
                    yield return new ValidationResult(
                        "O sêmen não deve ser informado para monta natural.",
                        new[] { nameof(SemenSampleId) });
            }
        }
    }
}
