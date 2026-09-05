using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalPregnancyStatus
    {
        [Description("Gestação confirmada")]
        Confirmed = 1,

        [Description("Parto realizado")]
        Calved = 2,

        [Description("Perda gestacional")]
        LostPregnancy = 3
    }
}
