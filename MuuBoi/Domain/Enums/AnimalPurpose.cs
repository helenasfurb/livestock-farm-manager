using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalPurpose
    {
        [Description("Matriz")]
        Breeder = 1,

        [Description("Novilha de Reposição")]
        ReplacementHeifer = 2,

        [Description("Vaca de Descarte")]
        CullCow = 3,

        [Description("Novilha para Venda")]
        HeiferForSale = 4
    }
}
