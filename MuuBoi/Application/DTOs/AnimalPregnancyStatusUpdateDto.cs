using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalPregnancyStatusUpdateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O status é obrigatório.")]
        public AnimalPregnancyStatus Status { get; set; }

        [Required(ErrorMessage = "A data da perda é obrigatória.")]
        public DateTime LossDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Status != AnimalPregnancyStatus.LostPregnancy)
                yield return new ValidationResult(
                    "Apenas o status 'Parto interrompido' pode ser definido manualmente. 'Parto realizado' é gerado automaticamente pelo sistema.",
                    new[] { nameof(Status) });

            if (LossDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data da perda não pode ser futura.",
                    new[] { nameof(LossDate) });
        }
    }
}
