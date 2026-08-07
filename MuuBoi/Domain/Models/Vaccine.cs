using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class Vaccine : BaseEntity, ITenantEntity
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Manufacturer { get; set; }

        public int? RecommendedIntervalDays { get; set; }

        public Guid PropertyId { get; set; }

        public ICollection<AnimalVaccination>? AnimalVaccinations { get; set; }
    }
}
