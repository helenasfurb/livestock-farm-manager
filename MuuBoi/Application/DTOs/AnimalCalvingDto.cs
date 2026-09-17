namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingDto
    {
        public int Id { get; set; }
        public int AnimalPregnancyId { get; set; }
        public DateTime CalvingDate { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public List<AnimalCalvingCalfDto> Calves { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
