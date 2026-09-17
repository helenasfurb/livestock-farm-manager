using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalPurpose
    {
        [Description("Matriz")]
        Breeder = 1,

        [Description("Novilha de reposição")]
        ReplacementHeifer = 2,

        [Description("Vaca de descarte")]
        CullCow = 3,

        [Description("Novilha para venda")]
        HeiferForSale = 4
    }
}
