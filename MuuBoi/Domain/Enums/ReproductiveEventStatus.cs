using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum ReproductiveEventStatus
    {
        [Description("Aguardando diagnóstico")]
        AwaitingDiagnosis = 1,

        [Description("Com gestação confirmada")]
        Successful = 2,

        [Description("Sem gestação")]
        Unsuccessful = 3
    }
}
