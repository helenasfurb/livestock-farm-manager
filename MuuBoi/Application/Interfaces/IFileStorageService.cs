namespace MuuBoi.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<bool> UploadAsync(string key, Stream content, string contentType);
        Task<bool> DeleteAsync(string key);
        Task<int> DeleteByPrefixAsync(string prefix);
        Uri GetReadUrl(string key, TimeSpan validity);
    }
}
