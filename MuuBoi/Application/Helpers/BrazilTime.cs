namespace MuuBoi.Application.Helpers
{
    public static class BrazilTime
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();

        public static DateTime ToLocalDate(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).Date;

        private static TimeZoneInfo ResolveZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
            }
        }
    }
}
