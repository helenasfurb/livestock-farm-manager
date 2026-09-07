using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Helpers
{
    /// <summary>
    /// Single source of truth for deriving an animal's <see cref="SanitaryStatus"/> (D8/D9).
    /// Pure per-animal function; the service gathers the facts set-based (no N+1), the same way
    /// reproductive/productive status are composed. Milk safety is a fact of the animal, resolved
    /// over its medications independently of any case status (D9).
    /// </summary>
    public static class AnimalSanitaryStatusResolver
    {
        /// <param name="hasCaseUnderTreatment">The animal has an active case that is medicated and not closed.</param>
        /// <param name="milkWithheldUntil">MAX future milk liberation over the animal's active medications (null = milk free).</param>
        /// <param name="hasSuspectedCase">The animal has an active case with no medication and not closed.</param>
        public static SanitaryStatus Resolve(
            bool hasCaseUnderTreatment,
            DateTime? milkWithheldUntil,
            bool hasSuspectedCase)
        {
            // Precedence: most actionable first.
            if (hasCaseUnderTreatment) return SanitaryStatus.UnderTreatment;
            if (milkWithheldUntil.HasValue) return SanitaryStatus.InWithdrawal;
            if (hasSuspectedCase) return SanitaryStatus.UnderObservation;
            return SanitaryStatus.Healthy;
        }
    }
}
