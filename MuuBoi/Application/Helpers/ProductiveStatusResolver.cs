using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    /// <summary>
    /// Single source of truth for deriving an animal's <see cref="ProductiveStatus"/> and the
    /// days-in-milk (DEL), mirroring <see cref="ReproductiveStatusResolver"/>. Used both by the
    /// animal detail (one animal) and the animal list (set-based), so both classify identically.
    /// Nothing here is persisted (Spec 11.2 D2/D5/D18).
    /// </summary>
    public static class ProductiveStatusResolver
    {
        /// <summary>In lactation on the reference date — closed interval [StartDate, EndDate] (D15).</summary>
        public static bool IsLactating(DateTime startDate, DateTime? endDate, DateTime reference)
        {
            var refDate = reference.Date;
            return startDate.Date <= refDate && (endDate == null || refDate <= endDate.Value.Date);
        }

        /// <summary>DEL: open → days since calving; closed → frozen at EndDate − StartDate (D5).</summary>
        public static int DaysInMilk(DateTime startDate, DateTime? endDate, DateTime reference)
            => endDate == null
                ? Math.Max(0, (reference.Date - startDate.Date).Days)
                : Math.Max(0, (endDate.Value.Date - startDate.Date).Days);

        /// <param name="activeLactations">The animal's active lactations (any IsActive == true).</param>
        public static ProductiveStatus Resolve(IEnumerable<Lactation> activeLactations, DateTime reference)
        {
            var list = activeLactations as ICollection<Lactation> ?? activeLactations.ToList();
            if (list.Any(l => IsLactating(l.StartDate, l.EndDate, reference)))
                return ProductiveStatus.Lactating;
            if (list.Count > 0)
                return ProductiveStatus.Dry;
            return ProductiveStatus.NeverLactated;
        }

        /// <summary>DEL of the currently-open lactation, or null when the animal is not lactating.</summary>
        public static int? CurrentDaysInMilk(IEnumerable<Lactation> activeLactations, DateTime reference)
        {
            var lactating = activeLactations.FirstOrDefault(l => IsLactating(l.StartDate, l.EndDate, reference));
            return lactating == null ? null : DaysInMilk(lactating.StartDate, lactating.EndDate, reference);
        }
    }
}
