using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalId { get; set; }
        public BodyConditionScore Score { get; set; }
        public string ScoreLabel { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
