using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class LactationCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "A data de início da lactação é obrigatória.")]
        public DateTime? StartDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });

            if (StartDate.HasValue && StartDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de início não pode ser futura.",
                    new[] { nameof(StartDate) });
        }
    }
}
