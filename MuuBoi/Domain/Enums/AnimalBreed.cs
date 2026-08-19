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
        Crossbred = 3
    }
}
