using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordDto
    {
        public int Id { get; set; }
        public BodyConditionScore Score { get; set; }
        public string ScoreLabel { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
