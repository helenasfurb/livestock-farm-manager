namespace MuuBoi.Application.Interfaces
{
    public interface IAccountService
    {
        Task<bool> DeletePropertyAsync(Guid propertyId);
    }
}
