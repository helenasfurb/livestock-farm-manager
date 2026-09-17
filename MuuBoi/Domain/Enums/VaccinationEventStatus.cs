using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    /// <summary>
    /// Derived status of a vaccination event — NEVER stored. Resolved at read time from the
    /// two dates against "now" (see VaccinationEventStatusResolver).
    /// </summary>
    public enum VaccinationEventStatus
    {
        [Description("Agendado")]
        Scheduled = 1,

        [Description("Vencido")]
        Overdue = 2,

        [Description("Aplicado")]
        Applied = 3
    }
}
