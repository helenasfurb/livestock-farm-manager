namespace MuuBoi.Application.Helpers
{
    public static class ReproductiveDashboardResolver
    {
        public const int VoluntaryWaitingPeriodDays = 45;

        public static (decimal? Rate, int Successful, int Diagnosed) ConceptionRate(int successful, int unsuccessful)
        {
            var diagnosed = successful + unsuccessful;
            var rate = diagnosed > 0
                ? Math.Round((decimal)successful / diagnosed * 100m, 1)
                : (decimal?)null;
            return (rate, successful, diagnosed);
        }

        public static bool IsEligibleForAI(
            DateTime? lastCalvingDate,
            bool hasActiveConfirmedPregnancy,
            DateTime? lastAwaitingBreedingDate,
            DateTime today,
            int voluntaryWaitingPeriodDays = VoluntaryWaitingPeriodDays)
        {
            if (hasActiveConfirmedPregnancy)
                return false;
            if (!lastCalvingDate.HasValue)
                return false;
            if (lastAwaitingBreedingDate.HasValue
                && lastAwaitingBreedingDate.Value.Date > lastCalvingDate.Value.Date)
                return false;
            return today.Date >= lastCalvingDate.Value.Date.AddDays(voluntaryWaitingPeriodDays);
        }

        public static decimal? ServicesPerConception(int totalServices, int conceptions)
            => conceptions > 0
                ? Math.Round((decimal)totalServices / conceptions, 2)
                : (decimal?)null;

        public static decimal? AverageCalvingIntervalDays(
            IEnumerable<IReadOnlyList<DateTime>> calvingDatesPerAnimalDescending,
            DateTime from,
            DateTime to)
        {
            var lowerBound = from.Date;
            var upperBound = to.Date;
            var intervals = new List<int>();

            foreach (var dates in calvingDatesPerAnimalDescending)
            {
                if (dates.Count < 2)
                    continue;
                var last = dates[0].Date;
                if (last < lowerBound || last > upperBound)
                    continue;
                var days = (last - dates[1].Date).Days;
                if (days > 0)
                    intervals.Add(days);
            }

            return intervals.Count > 0
                ? Math.Round((decimal)intervals.Average(), 1)
                : (decimal?)null;
        }

        public static decimal? AverageDaysOpen(
            IEnumerable<(int AnimalId, DateTime BreedingDate)> successfulBreedings,
            ILookup<int, DateTime> calvingDatesByAnimal)
        {
            var gaps = new List<int>();

            foreach (var (animalId, breedingDate) in successfulBreedings)
            {
                var priorCalving = calvingDatesByAnimal[animalId]
                    .Where(d => d.Date < breedingDate.Date)
                    .ToList();
                if (priorCalving.Count == 0)
                    continue;
                var days = (breedingDate.Date - priorCalving.Max().Date).Days;
                if (days > 0)
                    gaps.Add(days);
            }

            return gaps.Count > 0
                ? Math.Round((decimal)gaps.Average(), 1)
                : (decimal?)null;
        }
    }
}
