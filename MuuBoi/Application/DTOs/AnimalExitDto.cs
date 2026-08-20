using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalExitDto : IValidatableObject
    {
        [Required(ErrorMessage = "O motivo de saída é obrigatório.")]
        [ValidEnum(typeof(AnimalExitReason))]
        public AnimalExitReason ExitReason { get; set; }

        [Required(ErrorMessage = "A data de saída é obrigatória.")]
        public DateTime ExitDate { get; set; }

        [MaxLength(1000)]
        public string? ExitNotes { get; set; }

        [ValidEnum(typeof(AnimalDeathCause))]
        public AnimalDeathCause? DeathCause { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ExitDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de saída não pode ser futura.",
                    new[] { nameof(ExitDate) });

            if (ExitReason == AnimalExitReason.Death && !DeathCause.HasValue)
                yield return new ValidationResult(
                    "A causa da morte é obrigatória para saída por óbito.",
                    new[] { nameof(DeathCause) });

            if (ExitReason != AnimalExitReason.Death && DeathCause.HasValue)
                yield return new ValidationResult(
                    "A causa da morte só pode ser informada quando o motivo de saída for óbito.",
                    new[] { nameof(DeathCause) });
        }
    }
}
