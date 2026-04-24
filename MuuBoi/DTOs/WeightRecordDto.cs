namespace MuuBoi.DTOs
{
    public class WeightRecordDto
    {
        public int Id { get; set; }
        public decimal Weight { get; set; }
        public DateTime RecordedAt { get; set; }
        public string? Observations { get; set; }
    }
}
