using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class WeightRecordUpdateDto : IValidatableObject
    {
        [Range(0.01, 9999.99, ErrorMessage = "O peso deve ser maior que zero.")]
        public decimal? Weight { get; set; }

        public DateTime? WeightDate { get; set; }

        [MaxLength(500, ErrorMessage = "As observações da pesagem devem ter no máximo 500 caracteres.")]
        public string? WeightObservations { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WeightDate.HasValue && WeightDate.Value.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data da pesagem não pode ser futura.",
                    new[] { nameof(WeightDate) });
        }
    }
}
