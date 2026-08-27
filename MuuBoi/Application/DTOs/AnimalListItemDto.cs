namespace MuuBoi.Application.DTOs
{
    public class AnimalListItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string TagNumber { get; set; } = string.Empty;
        public string? PropertyTagNumber { get; set; }
        public EnumValueDto? Classification { get; set; }
        public EnumValueDto? Breed { get; set; }
        public EnumValueDto? ReproductiveStatus { get; set; }
        public bool IsActive { get; set; }
        public AnimalExitRecordDto? LastExitRecord { get; set; }
        public WeightRecordDto? LastWeightRecord { get; set; }
    }
}
