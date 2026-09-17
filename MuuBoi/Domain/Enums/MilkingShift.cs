using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum MilkingShift
    {
        [Description("Manhã")]
        Morning = 1,

        [Description("Tarde")]
        Afternoon = 2,

        [Description("Noite")]
        Evening = 3
    }
}
