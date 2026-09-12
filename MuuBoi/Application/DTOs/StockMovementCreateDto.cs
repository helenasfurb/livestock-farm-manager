using System.ComponentModel.DataAnnotations;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class StockMovementCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "O tipo de movimentação é obrigatório.")]
        public StockMovementType MovementType { get; set; }

        [Required(ErrorMessage = "O motivo da movimentação é obrigatório.")]
        public StockMovementReason MovementReason { get; set; }

        [Required(ErrorMessage = "A data da movimentação é obrigatória.")]
        public DateTime MovementDate { get; set; }

        [Required(ErrorMessage = "A quantidade é obrigatória.")]
        [Range(0.001, 9999999.999, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal Quantity { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "O valor total não pode ser negativo.")]
        public decimal? TotalValue { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "O valor unitário não pode ser negativo.")]
        public decimal? UnitPrice { get; set; }

        public ValueEntryMode? ValueEntryMode { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MovementDate.Date > DateTime.UtcNow.Date)
                yield return new ValidationResult(
                    "A data da movimentação não pode ser futura.",
                    new[] { nameof(MovementDate) });

            var expectedType = MovementReason switch
            {
                StockMovementReason.Purchase => StockMovementType.Input,
                StockMovementReason.OpeningBalance => StockMovementType.Input,
                StockMovementReason.Consumption => StockMovementType.Output,
                StockMovementReason.Loss => StockMovementType.Output,
                _ => (StockMovementType?)null
            };

            if (expectedType.HasValue && MovementType != expectedType.Value)
                yield return new ValidationResult(
                    $"O motivo '{MovementReason.GetDescription()}' exige movimentação do tipo '{expectedType.Value.GetDescription()}'.",
                    new[] { nameof(MovementType), nameof(MovementReason) });

            var isValuedEntry = MovementType == StockMovementType.Input &&
                (MovementReason == StockMovementReason.Purchase ||
                 MovementReason == StockMovementReason.OpeningBalance);

            if (isValuedEntry)
            {
                if (ValueEntryMode == Domain.Enums.ValueEntryMode.UnitPrice && !UnitPrice.HasValue)
                    yield return new ValidationResult(
                        "O valor unitário é obrigatório quando o modo de valor é 'Unitário'.",
                        new[] { nameof(UnitPrice) });

                if (ValueEntryMode != Domain.Enums.ValueEntryMode.UnitPrice && !TotalValue.HasValue)
                    yield return new ValidationResult(
                        "O valor total é obrigatório para compra ou saldo inicial.",
                        new[] { nameof(TotalValue) });
            }
        }
    }
}
