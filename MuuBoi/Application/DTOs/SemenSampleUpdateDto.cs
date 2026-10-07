using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleUpdateDto
    {
        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string? Name { get; set; }

        [MaxLength(100, ErrorMessage = "O registro do touro deve ter no máximo 100 caracteres.")]
        public string? BullRegistration { get; set; }

        [MaxLength(200, ErrorMessage = "A empresa de genética deve ter no máximo 200 caracteres.")]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        [MaxLength(100, ErrorMessage = "O número do lote deve ter no máximo 100 caracteres.")]
        public string? BatchNumber { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
