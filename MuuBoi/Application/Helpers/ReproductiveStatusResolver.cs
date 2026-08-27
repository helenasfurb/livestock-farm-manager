using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Helpers
{
    /// <summary>
    /// Single source of truth for deriving an animal's <see cref="ReproductiveStatus"/>.
    /// Used both by the animal detail (one animal) and the animal list (set-based, many animals),
    /// so both paths classify identically.
    /// </summary>
    public static class ReproductiveStatusResolver
    {
        public const int PostpartumDaysThreshold = 60;

        /// <param name="hasActiveConfirmedPregnancy">There is an active pregnancy with status Confirmed.</param>
        /// <param name="lastActiveCalvingDate">Date of the most recent active calving, if any.</param>
        /// <param name="lastActiveAwaitingBreedingDate">Breeding date of the most recent active event still awaiting diagnosis, if any.</param>
        /// <param name="utcNow">Reference "now" used to measure the postpartum window.</param>
        public static ReproductiveStatus Resolve(
            bool hasActiveConfirmedPregnancy,
            DateTime? lastActiveCalvingDate,
            DateTime? lastActiveAwaitingBreedingDate,
            DateTime utcNow)
        {
            if (hasActiveConfirmedPregnancy)
                return ReproductiveStatus.Pregnant;

            if (lastActiveCalvingDate.HasValue
                && (utcNow - lastActiveCalvingDate.Value).TotalDays < PostpartumDaysThreshold
                && (!lastActiveAwaitingBreedingDate.HasValue
                    || lastActiveAwaitingBreedingDate.Value <= lastActiveCalvingDate.Value))
                return ReproductiveStatus.Postpartum;

            return lastActiveAwaitingBreedingDate.HasValue
                ? ReproductiveStatus.AwaitingConfirmation
                : ReproductiveStatus.Open;
        }
    }
}
