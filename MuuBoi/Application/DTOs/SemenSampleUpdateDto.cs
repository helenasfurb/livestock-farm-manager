using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

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
    }
}
