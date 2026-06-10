using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class AnimalUpdateDto : IValidatableObject
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public int? BreedId { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }

        public bool? IsPregnant { get; set; }

        public DateTime? ExpectedBirthDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsPregnant == true && !ExpectedBirthDate.HasValue)
            {
                yield return new ValidationResult(
                    "A data prevista do nascimento é obrigatória se o animal estiver gestando.",
                    new[] { nameof(ExpectedBirthDate) }
                );
            }
            if (IsPregnant == false && ExpectedBirthDate.HasValue)
            {
                yield return new ValidationResult(
                    "A data prevista do nascimento não pode ser preenchida se o animal não estiver gestando.",
                    new[] { nameof(ExpectedBirthDate) }
                );
            }
        }
    }
}
