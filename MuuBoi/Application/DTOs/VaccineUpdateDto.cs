using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class VaccineUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Manufacturer { get; set; }

        public int? RecommendedIntervalDays { get; set; }
    }
}
