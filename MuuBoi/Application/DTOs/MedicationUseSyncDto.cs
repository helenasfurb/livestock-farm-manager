namespace MuuBoi.Application.DTOs
{
    public class MedicationUseSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public DateTime ApplicationDate { get; set; }
        public int? WithdrawalPeriodDays { get; set; }
        public string? Dose { get; set; }
        public string? Responsible { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
