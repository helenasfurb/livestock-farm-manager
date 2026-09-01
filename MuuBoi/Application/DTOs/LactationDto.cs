namespace MuuBoi.Application.DTOs
{
    public class LactationDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string? AnimalTagNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public EnumValueDto? Origin { get; set; }
        public int? CalvingId { get; set; }
        public string? DryOffNotes { get; set; }
        public bool IsLactating { get; set; }
        public int DaysInMilk { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
