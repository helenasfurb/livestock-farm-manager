using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordUpdateDto
    {
        [ValidEnum(typeof(BodyConditionScore))]
        public BodyConditionScore? Score { get; set; }
        public DateTime? RecordedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
