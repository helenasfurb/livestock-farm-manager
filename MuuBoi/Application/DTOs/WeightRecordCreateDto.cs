using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class WeightRecordCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "O peso é obrigatório.")]
        public decimal? Weight { get; set; }

        public DateTime? WeightDate { get; set; }

        [MaxLength(500, ErrorMessage = "As observações da pesagem devem ter no máximo 500 caracteres.")]
        public string? WeightObservations { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });
        }
    }
}
