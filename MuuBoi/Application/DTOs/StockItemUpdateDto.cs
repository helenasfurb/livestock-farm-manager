using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class StockItemUpdateDto
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        public int? StockCategoryId { get; set; }

        public int? UnitOfMeasureId { get; set; }

        [Range(0, 9999999.999, ErrorMessage = "O ponto crítico não pode ser negativo.")]
        public decimal? ReorderPoint { get; set; }

        [Range(1, 3650, ErrorMessage = "O tempo de reposição deve ser entre 1 e 3.650 dias.")]
        public int? ReplenishmentLeadDays { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
