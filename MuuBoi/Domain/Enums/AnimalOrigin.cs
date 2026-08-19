using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalOrigin
    {
        [Description("Nascido na Propriedade")]
        BornOnFarm = 1,

        [Description("Adquirido")]
        Purchased = 2
    }
}
