using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class MilkProductionCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Date { get; set; }

        public MilkingShift? Milking { get; set; }

        [Required(ErrorMessage = "O volume é obrigatório.")]
        [Range(0.01, 9999999.99, ErrorMessage = "O volume deve ser maior que zero.")]
        public decimal Volume { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Date > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data não pode ser futura.",
                    new[] { nameof(Date) });
        }
    }
}
