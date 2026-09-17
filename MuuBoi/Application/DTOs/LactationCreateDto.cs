using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class LactationCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data de início da lactação é obrigatória.")]
        public DateTime StartDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de início não pode ser futura.",
                    new[] { nameof(StartDate) });
        }
    }
}
