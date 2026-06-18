namespace MuuBoi.DTOs
{
    public class DashboardCardsDto
    {
        public int TotalAnimals { get; set; }
        public int PregnantAnimals { get; set; }
        public int ActiveTreatments { get; set; }
    }

    public class GenderDistributionDto
    {
        public string Gender { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BreedDistributionDto
    {
        public int BreedId { get; set; }
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

    public class BirthForecastDto
    {
        public int AnimalId { get; set; }
        public string AnimalName { get; set; } = string.Empty;
        public string? TagNumber { get; set; }
        public DateTime ExpectedBirthDate { get; set; }
    }

    public class DashboardDto
    {
        public DashboardCardsDto Cards { get; set; } = new();
        public IEnumerable<GenderDistributionDto> GenderDistribution { get; set; } = [];
        public IEnumerable<BreedDistributionDto> BreedDistribution { get; set; } = [];
        public IEnumerable<VaccinePerMonthDto> VaccinesPerMonth { get; set; } = [];
        public IEnumerable<BirthForecastDto> BirthForecast { get; set; } = [];
    }
}
