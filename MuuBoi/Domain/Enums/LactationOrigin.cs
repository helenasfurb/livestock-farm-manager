using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum LactationOrigin
    {
        [Description("Parto")]
        Calving = 1,

        [Description("Cadastro inicial")]
        InitialSeed = 2
    }
}
