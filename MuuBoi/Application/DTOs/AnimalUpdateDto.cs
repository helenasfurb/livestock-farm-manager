using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class AnimalUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public int? BreedId { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }
    }
}
