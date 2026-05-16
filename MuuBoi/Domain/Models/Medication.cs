using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class Medication : BaseEntity
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

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        public ICollection<AnimalMedication>? AnimalMedications { get; set; }
    }
}
