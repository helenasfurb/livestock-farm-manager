namespace MuuBoi.Application.DTOs
{
    public class AnimalPregnancyListItemDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string AnimalTagNumber { get; set; } = string.Empty;
        public string? AnimalName { get; set; }
        public int BreedingEventId { get; set; }
        public DateTime ConfirmationDate { get; set; }
        public DateTime ExpectedCalvingDate { get; set; }
        public DateTime? LossDate { get; set; }
        public EnumValueDto? Status { get; set; }
        public bool IsActive { get; set; }
    }
}
