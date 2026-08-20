using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalExitReason
    {
        [Description("Venda")]
        Sale = 1,

        [Description("Morte")]
        Death = 2,

        [Description("Descarte")]
        Discard = 3,

        [Description("Transferência")]
        Transfer = 4
    }
}
