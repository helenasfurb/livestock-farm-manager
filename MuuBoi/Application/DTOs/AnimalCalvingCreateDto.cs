using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data do parto é obrigatória.")]
        public DateTime CalvingDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public BodyConditionScore? BodyConditionScore { get; set; }

        [Required(ErrorMessage = "Informe ao menos uma cria.")]
        [MinLength(1, ErrorMessage = "Informe ao menos uma cria.")]
        public List<AnimalCalvingCalfCreateDto> Calves { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CalvingDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data do parto não pode ser futura.",
                    new[] { nameof(CalvingDate) });
        }
    }
}
