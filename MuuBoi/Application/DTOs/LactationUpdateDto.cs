using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class LactationUpdateDto : IValidatableObject
    {
        public DateTime? StartDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && StartDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de início não pode ser futura.",
                    new[] { nameof(StartDate) });
        }
    }
}
