using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class VaccineCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Manufacturer { get; set; }

        public int? RecommendedIntervalDays { get; set; }

        // Informational only: whether this vaccine requires a booster dose.
        public bool RequiresBooster { get; set; }
    }
}
