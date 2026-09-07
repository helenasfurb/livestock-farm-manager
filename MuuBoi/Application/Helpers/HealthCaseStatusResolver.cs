using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Helpers
{
    /// <summary>
    /// Single source of truth for deriving a <see cref="HealthCaseStatus"/> (D8). Used both for a
    /// single case (detail) and for set-based reads (list/history), so every path classifies identically.
    /// The milk withdrawal boundary is inclusive: released once <c>today &gt;= caseLiberationDate</c> (D6).
    /// </summary>
    public static class HealthCaseStatusResolver
    {
        /// <param name="hasMedication">The case has at least one active medication application.</param>
        /// <param name="resolvedAt">Explicit case closure; firms the withdrawal (D7).</param>
        /// <param name="caseLiberationDate">MAX(ApplicationDate + WithdrawalPeriodDays) over the case's active medications, if any.</param>
        /// <param name="utcNow">Reference "now".</param>
        public static HealthCaseStatus Resolve(
            bool hasMedication,
            DateTime? resolvedAt,
            DateTime? caseLiberationDate,
            DateTime utcNow)
        {
            if (!hasMedication)
                return resolvedAt.HasValue ? HealthCaseStatus.Resolved : HealthCaseStatus.Suspected;

            // Medicated, not yet closed → still treating (liberation is provisional).
            if (!resolvedAt.HasValue)
                return HealthCaseStatus.UnderTreatment;

            // Closed: inclusive withdrawal boundary (D6).
            if (caseLiberationDate.HasValue && utcNow.Date < caseLiberationDate.Value.Date)
                return HealthCaseStatus.InWithdrawal;

            return HealthCaseStatus.Resolved;
        }
    }
}
