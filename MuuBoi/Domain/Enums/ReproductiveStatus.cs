using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum ReproductiveStatus
    {
        [Description("Vazia")]
        Open = 1,

        [Description("Aguardando confirmação de prenhez")]
        AwaitingConfirmation = 2,

        [Description("Prenhe")]
        Pregnant = 3,

        [Description("Pós-parto")]
        Postpartum = 4
    }
}
