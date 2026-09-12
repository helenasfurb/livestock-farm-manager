using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum ValueEntryMode
    {
        [Description("Unitário")]
        UnitPrice = 1,

        [Description("Total")]
        TotalPrice = 2
    }
}
