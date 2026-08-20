using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BodyConditionRecordCreateDto
    {
        [Required(ErrorMessage = "O escore de condição corporal é obrigatório.")]
        public BodyConditionScore Score { get; set; }

        [Required(ErrorMessage = "A data da avaliação é obrigatória.")]
        public DateTime RecordedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
