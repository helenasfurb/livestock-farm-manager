using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class MedicationCreateDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
        public string? Description { get; set; }

        [MaxLength(100, ErrorMessage = "O fabricante deve ter no máximo 100 caracteres.")]
        public string? Manufacturer { get; set; }

        [MaxLength(100, ErrorMessage = "O princípio ativo deve ter no máximo 100 caracteres.")]
        public string? ActiveIngredient { get; set; }

        public int? DefaultWithdrawalPeriodDays { get; set; }
    }
}
