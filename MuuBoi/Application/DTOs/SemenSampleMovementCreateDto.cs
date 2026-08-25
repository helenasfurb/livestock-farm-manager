using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleMovementCreateDto
    {
        [Required(ErrorMessage = "O tipo de movimentação é obrigatório.")]
        public SemenMovementType MovementType { get; set; }

        [Required(ErrorMessage = "A data da movimentação é obrigatória.")]
        public DateTime MovementDate { get; set; }

        [Required(ErrorMessage = "A quantidade é obrigatória.")]
        [Range(1, 9999, ErrorMessage = "A quantidade deve ser entre 1 e 9.999.")]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
