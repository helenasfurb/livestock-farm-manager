namespace MuuBoi.Application.DTOs
{
    public class AnimalSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public string? Name { get; set; }
        public string? TagNumber { get; set; }
        public string? PropertyTagNumber { get; set; }
        public EnumValueDto? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public EnumValueDto? Breed { get; set; }
        public EnumValueDto? Classification { get; set; }
        public EnumValueDto? Purpose { get; set; }
        public EnumValueDto? Origin { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public IEnumerable<AnimalExitRecordDto> ExitRecords { get; set; } = new List<AnimalExitRecordDto>();
    }
}
