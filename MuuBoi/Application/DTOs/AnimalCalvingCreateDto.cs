using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        public Guid? LactationSyncId { get; set; }

        public Guid? BodyConditionSyncId { get; set; }

        [Required(ErrorMessage = "A data do parto é obrigatória.")]
        public DateTime? CalvingDate { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        [ValidEnum(typeof(BodyConditionScore))]
        public BodyConditionScore? BodyConditionScore { get; set; }

        [Required(ErrorMessage = "Informe ao menos uma cria.")]
        [MinLength(1, ErrorMessage = "Informe ao menos uma cria.")]
        public List<AnimalCalvingCalfCreateDto> Calves { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });

            if (LactationSyncId.HasValue && LactationSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização da lactação não pode ser vazio.",
                    new[] { nameof(LactationSyncId) });

            if (BodyConditionSyncId.HasValue && BodyConditionSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização do ECC não pode ser vazio.",
                    new[] { nameof(BodyConditionSyncId) });

            if (BodyConditionSyncId.HasValue && !BodyConditionScore.HasValue)
                yield return new ValidationResult(
                    "O identificador do ECC só se aplica quando o ECC é informado.",
                    new[] { nameof(BodyConditionSyncId) });

            if (CalvingDate.HasValue && CalvingDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data do parto não pode ser futura.",
                    new[] { nameof(CalvingDate) });
        }
    }
}
