using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleCreateDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BullRegistration { get; set; }

        [MaxLength(200)]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        [MaxLength(100)]
        public string? BatchNumber { get; set; }

        public DateTime? BatchDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Range(1, 9999, ErrorMessage = "A quantidade inicial deve ser entre 1 e 9.999.")]
        public int? InitialQuantity { get; set; }

        [MaxLength(500)]
        public string? InitialNotes { get; set; }
    }
}
