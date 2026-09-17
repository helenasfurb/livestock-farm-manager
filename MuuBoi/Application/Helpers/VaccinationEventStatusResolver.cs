using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Helpers
{
    /// <summary>
    /// Single source of truth for deriving a <see cref="VaccinationEventStatus"/> from the
    /// event's two dates against "now" (D4). Used both for a single event (detail) and for
    /// set-based reads (list/grid), so every path classifies identically.
    /// </summary>
    public static class VaccinationEventStatusResolver
    {
        /// <param name="applicationDate">Real application date, if the event was applied.</param>
        /// <param name="predictedDate">Scheduled date (agenda), if any.</param>
        /// <param name="utcNow">Reference "now".</param>
        public static VaccinationEventStatus Resolve(
            DateTime? applicationDate,
            DateTime? predictedDate,
            DateTime utcNow)
        {
            if (applicationDate.HasValue)
                return VaccinationEventStatus.Applied;

            if (predictedDate.HasValue && predictedDate.Value.Date >= utcNow.Date)
                return VaccinationEventStatus.Scheduled;

            return VaccinationEventStatus.Overdue;
        }
    }
}
