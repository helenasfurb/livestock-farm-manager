using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalOrigin
    {
        [Description("Nascido na propriedade")]
        BornOnFarm = 1,

        [Description("Adquirido")]
        Purchased = 2
    }
}
