using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class Breed : BaseEntity
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Animal>? Animals { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }
    }
}
