using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class BreedingEventUpdateDto : IValidatableObject
    {
        public DateTime? BreedingDate { get; set; }

        public int? SemenSampleId { get; set; }

        public int? SireAnimalId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (BreedingDate.HasValue && BreedingDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data da cobertura não pode ser futura.",
                    new[] { nameof(BreedingDate) });
        }
    }
}
