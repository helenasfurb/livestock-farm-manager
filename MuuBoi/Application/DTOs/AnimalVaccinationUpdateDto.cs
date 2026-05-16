using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalVaccinationUpdateDto
    {
        public int? VaccineId { get; set; }

        public DateTime? ApplicationDate { get; set; }

        public DateTime? NextApplicationDate { get; set; }

        [MaxLength(50)]
        public string? BatchNumber { get; set; }

        public decimal? DosageMl { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        [MaxLength(500)]
        public string? Observations { get; set; }
    }
}
