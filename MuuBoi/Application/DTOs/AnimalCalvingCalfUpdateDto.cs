using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfUpdateDto : IValidatableObject
    {
        [ValidEnum(typeof(AnimalGender))]
        public AnimalGender? Sex { get; set; }

        [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
        public decimal? WeightKg { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public Guid? WeightSyncId { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WeightSyncId.HasValue && WeightSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização da pesagem não pode ser vazio.",
                    new[] { nameof(WeightSyncId) });
        }
    }
}
