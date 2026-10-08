namespace MuuBoi.Application.Interfaces
{
    public interface IAccountRepository
    {
        Task<IReadOnlyList<string>> GetUserIdsByPropertyAsync(Guid propertyId);
        Task<bool> DeletePropertyAsync(Guid propertyId);
    }
}
