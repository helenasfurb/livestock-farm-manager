using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum StockMovementReason
    {
        [Description("Compra")]
        Purchase = 1,

        [Description("Saldo inicial")]
        OpeningBalance = 2,

        [Description("Consumo")]
        Consumption = 3,

        [Description("Perda")]
        Loss = 4,

        [Description("Ajuste")]
        Adjustment = 5
    }
}
