namespace MuuBoi.Application.DTOs
{
    public class HealthCaseSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalId { get; set; }
        public EnumValueDto? DiseaseType { get; set; }
        public string? DiseaseName { get; set; }
        public DateTime DiagnosisDate { get; set; }
        public IEnumerable<EnumValueDto> AffectedQuarters { get; set; } = new List<EnumValueDto>();
        public DateTime? ResolvedAt { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<MedicationUseSyncDto> Medications { get; set; } = new();
        public List<MastitisTestSyncDto> Tests { get; set; } = new();
    }
}
