using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalBreed
    {
        [Description("Holandesa")]
        Holstein = 1,

        [Description("Jersey")]
        Jersey = 2,

        [Description("Híbrida/Mestiça")]
        Crossbred = 3,

        [Description("Pardo Suíço")]
        BrownSwiss = 4,

        [Description("Simental")]
        Simmental = 5,

        [Description("Gir Leiteiro")]
        DairyGir = 6,

        [Description("Girolando")]
        Girolando = 7,

        [Description("Guzerá Leiteiro")]
        DairyGuzerat = 8,

        [Description("Sindi")]
        Sindhi = 9
    }
}
