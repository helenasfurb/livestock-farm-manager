using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum AnimalDeathCause
    {
        [Description("Doença")]
        Disease = 1,

        [Description("Acidente")]
        Accident = 2,

        [Description("Complicação Reprodutiva")]
        ReproductiveComplication = 3,

        [Description("Problema Digestivo")]
        DigestiveIssue = 4,

        [Description("Outros")]
        Other = 5
    }
}
