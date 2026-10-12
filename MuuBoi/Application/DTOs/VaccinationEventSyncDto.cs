namespace MuuBoi.Application.DTOs
{
    public class VaccinationEventSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int VaccineId { get; set; }
        public EnumValueDto? DoseType { get; set; }
        public DateTime? PredictedDate { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public int? ParentEventId { get; set; }
        public string? Notes { get; set; }
        public List<int> AnimalIds { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
