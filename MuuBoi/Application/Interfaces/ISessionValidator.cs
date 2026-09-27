namespace MuuBoi.Application.Interfaces
{
    public interface ISessionValidator
    {
        Task<bool> IsValidAsync(string userId, string securityStamp);
        void Invalidate(string userId);
    }
}
