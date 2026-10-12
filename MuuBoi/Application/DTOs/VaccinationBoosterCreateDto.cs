using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Input to create a booster dose from a parent event. Vaccine, animals and DoseType are
    /// inherited/assigned by the service; the only input is the predicted date of the new dose.
    /// </summary>
    public class VaccinationBoosterCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "A data prevista é obrigatória.")]
        public DateTime? PredictedDate { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });
        }
    }
}
