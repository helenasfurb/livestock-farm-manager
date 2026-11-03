using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    public static class GestationResolver
    {
        public const int GestationDays = 280;

        public static int GestationLength(AnimalPregnancy pregnancy, DateTime reference)
        {
            var conception = pregnancy.ExpectedCalvingDate.Date.AddDays(-GestationDays);
            var until = (EndDate(pregnancy) ?? reference).Date;
            return Math.Max(0, (until - conception).Days);
        }

        private static DateTime? EndDate(AnimalPregnancy pregnancy) => pregnancy.Status switch
        {
            AnimalPregnancyStatus.LostPregnancy => pregnancy.LossDate,
            AnimalPregnancyStatus.Calved => pregnancy.Calvings?.FirstOrDefault(c => c.IsActive)?.CalvingDate,
            _ => null
        };
    }
}
