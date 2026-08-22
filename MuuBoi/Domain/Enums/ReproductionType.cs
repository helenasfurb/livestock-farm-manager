using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum ReproductionType
    {
        [Description("Inseminação artificial")]
        ArtificialInsemination = 1,

        [Description("Monta natural")]
        NaturalMating = 2
    }
}
