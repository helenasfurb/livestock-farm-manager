using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Helpers
{
    public static class StockForecastResolver
    {
        public const int ConsumptionWindowDays = 30;

        public static decimal CurrentAverageUnitCost(decimal onHandQty, decimal onHandValue)
            => onHandQty > 0 ? onHandValue / onHandQty : 0m;

        public static decimal? DailyConsumptionRate(decimal consumedInWindow, int windowDays)
            => windowDays > 0 && consumedInWindow > 0 ? consumedInWindow / windowDays : (decimal?)null;

        public static (int? DaysOfCoverage, DateTime? RunOutDate) Forecast(
            decimal currentBalance,
            decimal? dailyRate,
            DateTime today)
        {
            if (!dailyRate.HasValue || dailyRate.Value <= 0)
                return (null, null);

            if (currentBalance <= 0)
                return (0, today.Date);

            var coverage = currentBalance / dailyRate.Value;
            var days = (int)Math.Floor(coverage);
            var runOut = today.Date.AddDays((double)coverage);
            return (days, runOut);
        }

        public static StockAlertSeverity ResolveSeverity(
            decimal currentBalance,
            decimal? reorderPoint,
            DateTime? runOutDate,
            int? replenishmentLeadDays,
            DateTime today)
        {
            if (!reorderPoint.HasValue || currentBalance > reorderPoint.Value)
                return StockAlertSeverity.Ok;

            if (currentBalance <= 0)
                return StockAlertSeverity.Critical;

            if (runOutDate.HasValue && replenishmentLeadDays.HasValue &&
                runOutDate.Value.Date < today.Date.AddDays(replenishmentLeadDays.Value))
                return StockAlertSeverity.Critical;

            return StockAlertSeverity.Attention;
        }
    }
}
