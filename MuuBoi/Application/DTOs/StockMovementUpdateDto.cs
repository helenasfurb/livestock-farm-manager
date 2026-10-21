using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class StockMovementUpdateDto
    {
        public DateTime? MovementDate { get; set; }

        [Range(0.001, 9999999.999, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal? Quantity { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "O valor total não pode ser negativo.")]
        public decimal? TotalValue { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
