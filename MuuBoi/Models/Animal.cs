using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{

    public class Animal
    {
        [Key]
        public int Id { get; set; }

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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}
