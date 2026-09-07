using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    /// <summary>
    /// Affected mammary quarters in a mastitis case. Flags — a case may affect more than one.
    /// Stored as a nullable int column on <c>HealthCase</c> (sparse: null for non-mastitis).
    /// </summary>
    [Flags]
    public enum Quarter
    {
        [Description("Anterior esquerdo")]
        FrontLeft = 1,

        [Description("Anterior direito")]
        FrontRight = 2,

        [Description("Posterior esquerdo")]
        RearLeft = 4,

        [Description("Posterior direito")]
        RearRight = 8
    }
}
