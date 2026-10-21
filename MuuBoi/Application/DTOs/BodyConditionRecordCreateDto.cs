using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "O escore de condição corporal é obrigatório.")]
        [ValidEnum(typeof(BodyConditionScore))]
        public BodyConditionScore? Score { get; set; }

        [Required(ErrorMessage = "A data da avaliação é obrigatória.")]
        public DateTime? RecordedAt { get; set; }

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
