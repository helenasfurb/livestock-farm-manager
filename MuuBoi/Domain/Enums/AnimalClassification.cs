using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalClassification
    {
        [Description("Bezerro(a)")]
        Calf = 1,

        [Description("Novilha")]
        Heifer = 2,

        [Description("Boi")]
        Steer = 3,

        [Description("Touro")]
        Bull = 4,

        [Description("Vaca")]
        Cow = 5
    }
}
