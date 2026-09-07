using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    /// <summary>
    /// Derived sanitary status of an animal — NEVER stored. A read-time rollup of the animal's
    /// active health cases and medications (see AnimalSanitaryStatusResolver).
    /// </summary>
    public enum SanitaryStatus
    {
        [Description("Saudável")]
        Healthy = 1,

        [Description("Em observação")]
        UnderObservation = 2,

        [Description("Em tratamento")]
        UnderTreatment = 3,

        [Description("Em carência")]
        InWithdrawal = 4
    }
}
