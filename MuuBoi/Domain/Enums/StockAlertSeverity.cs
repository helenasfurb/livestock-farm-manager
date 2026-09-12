using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum StockAlertSeverity
    {
        [Description("OK")]
        Ok = 1,

        [Description("Atenção")]
        Attention = 2,

        [Description("Crítico")]
        Critical = 3
    }
}
