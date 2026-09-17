namespace MuuBoi.Application.DTOs
{
    public class ProductiveDashboardDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal? AveragePerLactatingCow { get; set; }
        public ProductivePhaseDistributionDto PhaseDistribution { get; set; } = new();
    }

    public class ProductivePhaseDistributionDto
    {
        public int Lactating { get; set; }
        public int Dry { get; set; }
        public int NeverLactated { get; set; }
    }
}
