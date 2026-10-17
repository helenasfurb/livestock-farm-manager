using MuuBoi.Application.Interfaces;
using System.Security.Claims;

namespace MuuBoi.Api.Middleware
{
    public class UsageTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IUsageTracker _usageTracker;

        public UsageTrackingMiddleware(RequestDelegate next, IUsageTracker usageTracker)
        {
            _next = next;
            _usageTracker = usageTracker;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode is < 200 or >= 300)
                return;

            if (!Guid.TryParse(context.User.FindFirstValue("property_id"), out var propertyId))
                return;

            var method = context.Request.Method;

            if (HttpMethods.IsGet(method))
            {
                if (context.Request.Path.Value?.EndsWith("/changes", StringComparison.OrdinalIgnoreCase) == true)
                    _usageTracker.RecordPull(propertyId);
            }
            else if (HttpMethods.IsPost(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
            {
                _usageTracker.RecordPush(propertyId);
            }
        }
    }
}
