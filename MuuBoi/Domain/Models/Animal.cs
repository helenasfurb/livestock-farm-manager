using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class Animal : BaseEntity, ITenantEntity
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [Required]
        [MaxLength(6)]
        public string TagNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PropertyTagNumber { get; set; }

        public AnimalGender? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        public AnimalBreed? Breed { get; set; }

        public AnimalClassification? Classification { get; set; }

        public AnimalPurpose? Purpose { get; set; }

        public AnimalOrigin? Origin { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public ICollection<WeightRecord>? WeightRecords { get; set; }
        public ICollection<AnimalVaccination>? AnimalVaccinations { get; set; }
        public ICollection<AnimalMedication>? AnimalMedications { get; set; }
        public ICollection<BodyConditionRecord>? BodyConditionRecords { get; set; }
        public ICollection<AnimalExitRecord>? ExitRecords { get; set; }
    }
}
