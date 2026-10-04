using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class LactationDryOffDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data da secagem é obrigatória.")]
        public DateTime EndDate { get; set; }

        [MaxLength(500, ErrorMessage = "As observações da secagem devem ter no máximo 500 caracteres.")]
        public string? DryOffNotes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data da secagem não pode ser futura.",
                    new[] { nameof(EndDate) });
        }
    }
}
