namespace MuuBoi.Application.DTOs
{
    public class VaccineDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Informational only: whether this vaccine requires a booster dose.
        public bool RequiresBooster { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
