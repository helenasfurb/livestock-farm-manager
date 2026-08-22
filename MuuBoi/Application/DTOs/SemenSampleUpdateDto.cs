using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? BullName { get; set; }

        [MaxLength(100)]
        public string? BullRegistration { get; set; }

        [MaxLength(200)]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        public DateTime? CollectedAt { get; set; }

        public DateTime? ManufacturedAt { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
