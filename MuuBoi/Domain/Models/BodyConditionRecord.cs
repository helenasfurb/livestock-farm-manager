using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class BodyConditionRecord : BaseEntity
    {
        public int AnimalId { get; set; }

        public BodyConditionScore Score { get; set; }

        public DateTime RecordedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Animal? Animal { get; set; }
    }
}
