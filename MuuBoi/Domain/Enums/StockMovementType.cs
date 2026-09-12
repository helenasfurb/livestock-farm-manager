using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum StockMovementType
    {
        [Description("Entrada")]
        Input = 1,

        [Description("Saída")]
        Output = 2
    }
}
