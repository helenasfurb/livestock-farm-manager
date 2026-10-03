using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalUpdateDto : IValidatableObject
    {
        [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
        public string? TagNumber { get; set; }

        [MaxLength(100, ErrorMessage = "O brinco da fazenda deve ter no máximo 100 caracteres.")]
        public string? PropertyTagNumber { get; set; }

        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string? Name { get; set; }

        [ValidEnum(typeof(AnimalGender))]
        public AnimalGender? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [ValidEnum(typeof(AnimalBreed))]
        public AnimalBreed? Breed { get; set; }

        [ValidEnum(typeof(AnimalClassification))]
        public AnimalClassification? Classification { get; set; }

        [ValidEnum(typeof(AnimalPurpose))]
        public AnimalPurpose? Purpose { get; set; }

        [ValidEnum(typeof(AnimalOrigin))]
        public AnimalOrigin? Origin { get; set; }

        [MaxLength(1000, ErrorMessage = "As observações devem ter no máximo 1000 caracteres.")]
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
