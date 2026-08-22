namespace MuuBoi.Application.DTOs
{
    public class BreedingEventListItemDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string AnimalTagNumber { get; set; } = string.Empty;
        public EnumValueDto? ReproductionType { get; set; }
        public DateTime BreedingDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public int ServiceNumber { get; set; }
    }
}
