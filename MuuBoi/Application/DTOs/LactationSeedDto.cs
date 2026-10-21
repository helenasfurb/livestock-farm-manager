using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Bloco opcional embutido no cadastro do animal (D17) para semear a última lactação
    /// de uma Vaca/Novilha. Com EndDate → lactação fechada (seca); sem EndDate → aberta.
    /// </summary>
    public class LactationSeedDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data de início da lactação é obrigatória.")]
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && StartDate.Value > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de início não pode ser futura.",
                    new[] { nameof(StartDate) });

            if (EndDate.HasValue)
            {
                if (EndDate.Value > DateTime.UtcNow)
                    yield return new ValidationResult(
                        "A data da secagem não pode ser futura.",
                        new[] { nameof(EndDate) });

                if (StartDate.HasValue && EndDate.Value < StartDate.Value)
                    yield return new ValidationResult(
                        "A data da secagem não pode ser anterior ao início da lactação.",
                        new[] { nameof(EndDate) });
            }
        }
    }
}
