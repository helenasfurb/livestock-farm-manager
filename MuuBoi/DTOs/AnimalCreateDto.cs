using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class AnimalCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }

        public int? BreedId { get; set; }

        public decimal? InitialWeight { get; set; }

        public DateTime? InitialWeightDate { get; set; }

        [MaxLength(500)]
        public string? InitialWeightObservations { get; set; }
    }
}