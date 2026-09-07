using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    /// <summary>
    /// Derived status of a health case — NEVER stored. Resolved at read time from the case's
    /// medications, its explicit closure (<c>ResolvedAt</c>) and the milk withdrawal against
    /// "now" (see HealthCaseStatusResolver).
    /// </summary>
    public enum HealthCaseStatus
    {
        [Description("Em observação")]
        Suspected = 1,

        [Description("Em tratamento")]
        UnderTreatment = 2,

        [Description("Em carência")]
        InWithdrawal = 3,

        [Description("Resolvido")]
        Resolved = 4
    }
}
