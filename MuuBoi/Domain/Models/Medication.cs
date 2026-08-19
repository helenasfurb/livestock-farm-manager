using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class Medication : BaseEntity, ITenantEntity
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Manufacturer { get; set; }

        [MaxLength(100)]
        public string? ActiveIngredient { get; set; }

        public int? DefaultWithdrawalPeriodDays { get; set; }

        public Guid PropertyId { get; set; }

        public ICollection<AnimalMedication>? AnimalMedications { get; set; }
    }
}
