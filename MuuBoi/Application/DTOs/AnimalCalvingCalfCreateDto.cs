using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfCreateDto : IValidatableObject
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "O sexo da cria é obrigatório.")]
        public AnimalGender Sex { get; set; }

        [ValidEnum(typeof(AnimalBreed))]
        public AnimalBreed? Breed { get; set; }

        [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
        public decimal? WeightKg { get; set; }

        [Required(ErrorMessage = "O status vital da cria é obrigatório.")]
        public CalfVitalStatus VitalStatus { get; set; }

        // Brinco oficial: alfanumérico, sem padrão definido; limite de 6 caracteres da coluna Animals.TagNumber.
        [MaxLength(6, ErrorMessage = "O brinco deve ter no máximo 6 caracteres.")]
        public string? TagNumber { get; set; }

        [MaxLength(100)]
        public string? PropertyTagNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (VitalStatus == CalfVitalStatus.Live && string.IsNullOrWhiteSpace(Name))
                yield return new ValidationResult(
                    "O nome é obrigatório para crias nascidas vivas.",
                    new[] { nameof(Name) });

            if (VitalStatus == CalfVitalStatus.Live && !Breed.HasValue)
                yield return new ValidationResult(
                    "A raça é obrigatória para crias nascidas vivas.",
                    new[] { nameof(Breed) });

            if (VitalStatus != CalfVitalStatus.Live &&
                (!string.IsNullOrWhiteSpace(TagNumber) || !string.IsNullOrWhiteSpace(PropertyTagNumber)))
                yield return new ValidationResult(
                    "Brinco e brinco de fazenda só se aplicam a crias nascidas vivas.",
                    new[] { nameof(TagNumber), nameof(PropertyTagNumber) });
        }
    }
}
