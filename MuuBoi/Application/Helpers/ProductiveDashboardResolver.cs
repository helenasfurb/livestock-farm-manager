namespace MuuBoi.Application.Helpers
{
    public static class ProductiveDashboardResolver
    {
        public static decimal? AveragePerLactatingCow(decimal totalVolume, int lactatingCows)
            => lactatingCows > 0 ? totalVolume / lactatingCows : (decimal?)null;
    }
}
