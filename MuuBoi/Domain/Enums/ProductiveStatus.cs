using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum ProductiveStatus
    {
        [Description("Nunca lactou")]
        NeverLactated = 1,

        [Description("Em lactação")]
        Lactating = 2,

        [Description("Seca")]
        Dry = 3
    }
}
