using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class BreedCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
