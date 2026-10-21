using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleMovementCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "O tipo de movimentação é obrigatório.")]
        public SemenMovementType MovementType { get; set; }

        [Required(ErrorMessage = "A data da movimentação é obrigatória.")]
        public DateTime MovementDate { get; set; }

        [Required(ErrorMessage = "A quantidade é obrigatória.")]
        [Range(1, 9999, ErrorMessage = "A quantidade deve ser entre 1 e 9.999.")]
        public int Quantity { get; set; }

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
