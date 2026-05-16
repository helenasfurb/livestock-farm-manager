namespace MuuBoi.DTOs
{
    public class VaccineDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public int? RecommendedIntervalDays { get; set; }
    }
}
