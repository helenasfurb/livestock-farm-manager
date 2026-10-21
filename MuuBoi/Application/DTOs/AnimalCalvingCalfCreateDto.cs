using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        public Guid? AnimalSyncId { get; set; }

        public Guid? WeightSyncId { get; set; }

        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "O sexo da cria é obrigatório.")]
        [ValidEnum(typeof(AnimalGender))]
        public AnimalGender? Sex { get; set; }

        [ValidEnum(typeof(AnimalBreed))]
        public AnimalBreed? Breed { get; set; }

        [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
        public decimal? WeightKg { get; set; }

        [Required(ErrorMessage = "O status vital da cria é obrigatório.")]
        [ValidEnum(typeof(CalfVitalStatus))]
        public CalfVitalStatus? VitalStatus { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
        public string? TagNumber { get; set; }

        [MaxLength(100, ErrorMessage = "O brinco da fazenda deve ter no máximo 100 caracteres.")]
        public string? PropertyTagNumber { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização da cria não pode ser vazio.",
                    new[] { nameof(SyncId) });

            if (AnimalSyncId.HasValue && AnimalSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização do animal da cria não pode ser vazio.",
                    new[] { nameof(AnimalSyncId) });

            if (WeightSyncId.HasValue && WeightSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização da pesagem não pode ser vazio.",
                    new[] { nameof(WeightSyncId) });

            if (AnimalSyncId.HasValue && VitalStatus != CalfVitalStatus.Live)
                yield return new ValidationResult(
                    "O identificador do animal só se aplica a crias nascidas vivas.",
                    new[] { nameof(AnimalSyncId) });

            if (WeightSyncId.HasValue && (VitalStatus != CalfVitalStatus.Live || !WeightKg.HasValue))
                yield return new ValidationResult(
                    "O identificador da pesagem só se aplica a crias vivas com peso informado.",
                    new[] { nameof(WeightSyncId) });

            if (VitalStatus == CalfVitalStatus.Live && string.IsNullOrWhiteSpace(Name))
                yield return new ValidationResult(
                    "O nome é obrigatório para crias nascidas vivas.",
                    new[] { nameof(Name) });

            if (VitalStatus == CalfVitalStatus.Live && !Breed.HasValue)
                yield return new ValidationResult(
                    "A raça é obrigatória para crias nascidas vivas.",
                    new[] { nameof(Breed) });

            if (VitalStatus.HasValue && VitalStatus != CalfVitalStatus.Live &&
                (!string.IsNullOrWhiteSpace(TagNumber) || !string.IsNullOrWhiteSpace(PropertyTagNumber)))
                yield return new ValidationResult(
                    "Brinco e brinco de fazenda só se aplicam a crias nascidas vivas.",
                    new[] { nameof(TagNumber), nameof(PropertyTagNumber) });
        }
    }
}
