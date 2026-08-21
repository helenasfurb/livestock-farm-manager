namespace MuuBoi.Application.DTOs
{
    public class AnimalExitRecordDto
    {
        public int Id { get; set; }
        public EnumValueDto? ExitReason { get; set; }
        public DateTime ExitDate { get; set; }
        public string? ExitNotes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
