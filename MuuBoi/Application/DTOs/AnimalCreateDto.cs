using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O brinco principal é obrigatório.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
        public string TagNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PropertyTagNumber { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "O sexo é obrigatório.")]
        [ValidEnum(typeof(AnimalGender))]
        public AnimalGender? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [ValidEnum(typeof(AnimalBreed))]
        public AnimalBreed? Breed { get; set; }

        [Required(ErrorMessage = "A classificação é obrigatória.")]
        [ValidEnum(typeof(AnimalClassification))]
        public AnimalClassification? Classification { get; set; }

        [ValidEnum(typeof(AnimalPurpose))]
        public AnimalPurpose? Purpose { get; set; }

        [ValidEnum(typeof(AnimalOrigin))]
        public AnimalOrigin? Origin { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [ValidEnum(typeof(BodyConditionScore))]
        public BodyConditionScore? InitialBodyConditionScore { get; set; }

        public DateTime? InitialBodyConditionDate { get; set; }

        [MaxLength(500)]
        public string? InitialBodyConditionNotes { get; set; }

        public decimal? InitialWeight { get; set; }

        public DateTime? InitialWeightDate { get; set; }

        [MaxLength(500)]
        public string? InitialWeightObservations { get; set; }

        // Bloco opcional de última lactação (Spec 11.2 D17) — só para Vaca/Novilha (validado no AnimalService).
        public LactationSeedDto? InitialLactation { get; set; }

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

            if (InitialBodyConditionDate.HasValue && InitialBodyConditionDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data do ECC inicial não pode ser futura.",
                    new[] { nameof(InitialBodyConditionDate) });
        }
    }
}
