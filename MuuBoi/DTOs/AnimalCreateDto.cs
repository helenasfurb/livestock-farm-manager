using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class AnimalCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Breed { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }

        public bool IsActive { get; set; } = true;
    }
}