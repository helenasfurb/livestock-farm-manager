namespace MuuBoi.Application.Interfaces
{
    public interface IUsageService
    {
        Task<bool> RecordLoginAsync(string userId, Guid propertyId);
        Task<bool> FlushAsync();
        Task<bool> PurgeLoginEventsAsync();
    }
}
