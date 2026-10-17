using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IPortalAuthService
    {
        PortalTokenDto Login(PortalLoginDto dto);
    }
}
