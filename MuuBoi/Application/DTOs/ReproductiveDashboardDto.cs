namespace MuuBoi.Application.DTOs
{
    public class ReproductiveDashboardDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public ConceptionRateDto ConceptionRate { get; set; } = new();
        public ReproductiveStatusDistributionDto StatusDistribution { get; set; } = new();
        public List<AiEligibleAnimalDto> EligibleForAi { get; set; } = new();
        public ConceptionRateDto FirstServiceConceptionRate { get; set; } = new();
        public decimal? ServicesPerConception { get; set; }
        public decimal? AverageDaysOpen { get; set; }
        public decimal? AverageCalvingIntervalDays { get; set; }
        public int LostPregnancies { get; set; }
    }

    public class ConceptionRateDto
    {
        public decimal? Rate { get; set; }
        public int Successful { get; set; }
        public int Diagnosed { get; set; }
        public int AwaitingDiagnosis { get; set; }
    }

    public class ReproductiveStatusDistributionDto
    {
        public int Open { get; set; }
        public int AwaitingConfirmation { get; set; }
        public int Pregnant { get; set; }
        public int Postpartum { get; set; }
    }

    public class AiEligibleAnimalDto
    {
        public int AnimalId { get; set; }
        public string? Name { get; set; }
        public string? TagNumber { get; set; }
        public DateTime? LastCalvingDate { get; set; }
    }
}
