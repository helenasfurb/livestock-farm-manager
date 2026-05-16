using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuuBoi.Models
{
    public class AnimalVaccination : BaseEntity
    {
        [Required]
        public int AnimalId { get; set; }

        [Required]
        public int VaccineId { get; set; }

        public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;

        public DateTime? NextApplicationDate { get; set; }

        [MaxLength(50)]
        public string? BatchNumber { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal? DosageMl { get; set; }

        [MaxLength(100)]
        public string? Responsible { get; set; }

        [MaxLength(500)]
        public string? Observations { get; set; }

        public Animal? Animal { get; set; }
        public Vaccine? Vaccine { get; set; }
    }
}
