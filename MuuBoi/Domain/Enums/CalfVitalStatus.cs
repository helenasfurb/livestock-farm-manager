using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum CalfVitalStatus
    {
        [Description("Nascido vivo")]
        Live = 1,

        [Description("Natimorto")]
        Stillborn = 2
    }
}
