using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalUpdateDto : IValidatableObject
    {
        [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
        public string? TagNumber { get; set; }

        [MaxLength(100)]
        public string? PropertyTagNumber { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        public AnimalGender? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        public AnimalBreed? Breed { get; set; }

        public AnimalClassification? Classification { get; set; }

        public AnimalPurpose? Purpose { get; set; }

        public AnimalOrigin? Origin { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Classification.HasValue && Gender.HasValue)
            {
                var femaleOnly = new[] { AnimalClassification.Heifer, AnimalClassification.Cow };
                var maleOnly = new[] { AnimalClassification.Steer, AnimalClassification.Bull };

                if (femaleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.F)
                    yield return new ValidationResult(
                        $"A classificação '{Classification.Value.GetDescription()}' é exclusiva de fêmeas.",
                        new[] { nameof(Classification) });

                if (maleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.M)
                    yield return new ValidationResult(
                        $"A classificação '{Classification.Value.GetDescription()}' é exclusiva de machos.",
                        new[] { nameof(Classification) });
            }
        }
    }
}
