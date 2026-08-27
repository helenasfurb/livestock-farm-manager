using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfCreateDto
    {
        [Required(ErrorMessage = "O sexo da cria é obrigatório.")]
        public AnimalGender Sex { get; set; }

        [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
        public decimal? WeightKg { get; set; }

        [Required(ErrorMessage = "O status vital da cria é obrigatório.")]
        public CalfVitalStatus VitalStatus { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
