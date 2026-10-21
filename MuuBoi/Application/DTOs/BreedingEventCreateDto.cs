using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BreedingEventCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "O tipo de reprodução é obrigatório.")]
        [ValidEnum(typeof(ReproductionType))]
        public ReproductionType? ReproductionType { get; set; }

        [Required(ErrorMessage = "A data da cobertura é obrigatória.")]
        public DateTime? BreedingDate { get; set; }

        public int? SemenSampleId { get; set; }

        public int? SireAnimalId { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });

            if (BreedingDate.HasValue && BreedingDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data da cobertura não pode ser futura.",
                    new[] { nameof(BreedingDate) });

            if (ReproductionType == Domain.Enums.ReproductionType.ArtificialInsemination)
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

            if (ReproductionType == Domain.Enums.ReproductionType.NaturalMating)
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
