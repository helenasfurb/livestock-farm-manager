using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleMovementUpdateDto
    {
        public DateTime? MovementDate { get; set; }

        [Range(1, 9999, ErrorMessage = "A quantidade deve ser entre 1 e 9.999.")]
        public int? Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
