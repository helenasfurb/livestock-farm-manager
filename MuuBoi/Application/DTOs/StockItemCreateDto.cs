using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class StockItemCreateDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "A categoria é obrigatória.")]
        public int StockCategoryId { get; set; }

        [Required(ErrorMessage = "A unidade de medida é obrigatória.")]
        public int UnitOfMeasureId { get; set; }

        [Range(0, 9999999.999, ErrorMessage = "O ponto crítico não pode ser negativo.")]
        public decimal? ReorderPoint { get; set; }

        [Range(1, 3650, ErrorMessage = "O tempo de reposição deve ser entre 1 e 3.650 dias.")]
        public int? ReplenishmentLeadDays { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Range(0.001, 9999999.999, ErrorMessage = "A quantidade inicial deve ser maior que zero.")]
        public decimal? InitialQuantity { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "O valor inicial não pode ser negativo.")]
        public decimal? InitialTotalValue { get; set; }

        [MaxLength(500)]
        public string? InitialNotes { get; set; }
    }
}
