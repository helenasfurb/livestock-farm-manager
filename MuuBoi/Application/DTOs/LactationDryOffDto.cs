using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class LactationDryOffDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data da secagem é obrigatória.")]
        public DateTime EndDate { get; set; }

        [MaxLength(500)]
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
