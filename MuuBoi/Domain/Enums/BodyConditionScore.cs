using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum BodyConditionScore
    {
        [Description("Magreza Severa")]
        SeverelyThin = 1,

        [Description("Estrutura Óssea Visível")]
        Thin = 2,

        [Description("Estrutura Óssea e Cobertura Bem Distribuídas")]
        Ideal = 3,

        [Description("Cobertura Predominante sobre Estrutura Óssea")]
        Fleshy = 4,

        [Description("Obesidade Severa")]
        SeverelyObese = 5
    }
}
