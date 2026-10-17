using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IUsageRepository
    {
        Task<bool> AddLoginEventAsync(LoginEvent loginEvent);
        Task<bool> AddUsageAsync(UsageCountDto count);
        Task<int> DeleteLoginEventsBeforeAsync(DateTime cutoffUtc);
    }
}
