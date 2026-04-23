using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class Animal : BaseEntity
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int? BreedId { get; set; }

        public Breed? Breed { get; set; }
    }
}
