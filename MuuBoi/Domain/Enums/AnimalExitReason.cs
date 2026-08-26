using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalExitReason
    {
        [Description("Venda")]
        Sale = 1,

        [Description("Morte natural")]
        NaturalDeath = 2,

        [Description("Consumo próprio")]
        OwnConsumption = 3
    }
}
