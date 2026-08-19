using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class Animal : BaseEntity, ITenantEntity
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public AnimalGender? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? TagNumber { get; set; }

        public Guid PropertyId { get; set; }

        public int? BreedId { get; set; }

        public bool IsPregnant { get; set; } = false;

        public DateTime? ExpectedBirthDate { get; set; }

        public Breed? Breed { get; set; }

        public ICollection<WeightRecord>? WeightRecords { get; set; }
        public ICollection<AnimalVaccination>? AnimalVaccinations { get; set; }
        public ICollection<AnimalMedication>? AnimalMedications { get; set; }
    }
}
