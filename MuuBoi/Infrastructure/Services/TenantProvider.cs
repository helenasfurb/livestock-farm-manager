using MuuBoi.Application.Interfaces;
using System.Security.Claims;

namespace MuuBoi.Infrastructure.Services
{
    public class TenantProvider(IHttpContextAccessor accessor) : ITenantProvider
    {
        public Guid PropertyId =>
            Guid.TryParse(accessor.HttpContext?.User.FindFirst("property_id")?.Value, out var id)
                ? id : Guid.Empty;
    }
}
