namespace MuuBoi.Application.DTOs
{
    public class WeightRecordDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalId { get; set; }
        public decimal Weight { get; set; }
        public DateTime RecordedAt { get; set; }
        public string? Observations { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
