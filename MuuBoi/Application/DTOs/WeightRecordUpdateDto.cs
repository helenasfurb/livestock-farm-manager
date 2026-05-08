using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class WeightRecordUpdateDto
    {
        public decimal? Weight { get; set; }

        public DateTime? WeightDate { get; set; }

        [MaxLength(500)]
        public string? WeightObservations { get; set; }
    }
}
