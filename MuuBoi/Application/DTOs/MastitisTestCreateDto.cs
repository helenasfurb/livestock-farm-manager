using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    /// <summary>Append a mastitis test to a health case (POST /health-cases/{id}/tests).</summary>
    public class MastitisTestCreateDto : IValidatableObject
    {
        public MastitisTestType TestType { get; set; }

        [Required(ErrorMessage = "O resultado é obrigatório.")]
        [MaxLength(200)]
        public string Result { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data do teste é obrigatória.")]
        public DateTime TestDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(typeof(MastitisTestType), TestType))
                yield return new ValidationResult(
                    "Informe o tipo de teste.",
                    new[] { nameof(TestType) });

            if (TestDate.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data do teste não pode ser futura.",
                    new[] { nameof(TestDate) });
        }
    }
}
