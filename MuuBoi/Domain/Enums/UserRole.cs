using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum UserRole
    {
        [Description("Administrador")]
        Admin = 1,

        [Description("Membro")]
        Member = 2
    }
}
