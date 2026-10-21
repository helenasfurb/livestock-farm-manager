namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCalfSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int? AnimalId { get; set; }
        public EnumValueDto? Sex { get; set; }
        public decimal? WeightKg { get; set; }
        public EnumValueDto? VitalStatus { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
