using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class DashboardDto
    {
        public HerdCompositionDto Herd { get; set; } = new();
        public SanitaryPulseDto Sanitary { get; set; } = new();
        public IEnumerable<VaccinePerMonthDto> VaccinesPerMonth { get; set; } = [];
    }

    public class HerdCompositionDto
    {
        public int TotalAnimals { get; set; }
        public IEnumerable<ClassificationDistributionDto> ClassificationDistribution { get; set; } = [];
        public IEnumerable<GenderDistributionDto> GenderDistribution { get; set; } = [];
        public IEnumerable<BreedDistributionDto> BreedDistribution { get; set; } = [];
    }

    public class SanitaryPulseDto
    {
        public AnimalsUnderTreatmentDto UnderTreatment { get; set; } = new();
        public OverdueVaccinationsDto OverdueVaccinations { get; set; } = new();
    }

    public class ClassificationDistributionDto
    {
        public AnimalClassification Classification { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class GenderDistributionDto
    {
        public string Gender { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BreedDistributionDto
    {
        public AnimalBreed Breed { get; set; }
        public string BreedName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class VaccinePerMonthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AnimalsUnderTreatmentDto
    {
        public int Count { get; set; }
        public IEnumerable<AnimalListItemDto> Animals { get; set; } = [];
    }

    public class OverdueVaccinationsDto
    {
        public int Count { get; set; }
        public IEnumerable<OverdueVaccinationItemDto> Events { get; set; } = [];
    }

    public class OverdueVaccinationItemDto
    {
        public int VaccinationEventId { get; set; }
        public string VaccineName { get; set; } = string.Empty;
        public DateTime PredictedDate { get; set; }
        public int AnimalCount { get; set; }
    }
}
