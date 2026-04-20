namespace MuuBoi.DTOs
{
    public class AnimalDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Breed { get; set; }

        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        public string? TagNumber { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
