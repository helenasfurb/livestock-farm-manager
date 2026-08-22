namespace MuuBoi.Application.DTOs
{
    public class AnimalDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string TagNumber { get; set; } = string.Empty;
        public string? PropertyTagNumber { get; set; }
        public EnumValueDto? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public EnumValueDto? Breed { get; set; }
        public EnumValueDto? Classification { get; set; }
        public EnumValueDto? Purpose { get; set; }
        public EnumValueDto? Origin { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public AnimalExitRecordDto? LastExitRecord { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public WeightRecordDto? LastWeightRecord { get; set; }
        public IEnumerable<WeightRecordDto>? WeightRecords { get; set; }
        public BodyConditionRecordDto? LastBodyConditionRecord { get; set; }
        public EnumValueDto? ReproductiveStatus { get; set; }
    }
}
