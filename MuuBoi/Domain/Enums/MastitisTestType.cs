using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum MastitisTestType
    {
        [Description("Fundo preto")]
        BlackBottomCup = 1,

        [Description("CMT")]
        CMT = 2,

        [Description("CCS")]
        CCS = 3,

        [Description("Microbiológico")]
        Microbiological = 4
    }
}
