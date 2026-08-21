using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordUpdateDto
    {
        public BodyConditionScore? Score { get; set; }
        public DateTime? RecordedAt { get; set; }
    }
}
