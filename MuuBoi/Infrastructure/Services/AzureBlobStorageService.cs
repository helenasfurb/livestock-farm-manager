using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Options;

namespace MuuBoi.Infrastructure.Services
{
    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly Lazy<BlobContainerClient> _container;

        public AzureBlobStorageService(IConfiguration configuration, IOptions<BlobStorageOptions> options)
        {
            _container = new Lazy<BlobContainerClient>(() =>
            {
                var connectionString = configuration.GetConnectionString("BlobStorage");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("Blob Storage connection string is not configured.");

                return new BlobServiceClient(connectionString).GetBlobContainerClient(options.Value.ContainerName);
            });
        }

        public async Task<bool> UploadAsync(string key, Stream content, string contentType)
        {
            var blob = _container.Value.GetBlobClient(key);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            });
            return true;
        }

        public async Task<bool> DeleteAsync(string key)
        {
            var response = await _container.Value.DeleteBlobIfExistsAsync(key);
            return response.Value;
        }

        public async Task<int> DeleteByPrefixAsync(string prefix)
        {
            var deleted = 0;
            await foreach (var item in _container.Value.GetBlobsAsync(new GetBlobsOptions { Prefix = prefix }))
            {
                var response = await _container.Value.DeleteBlobIfExistsAsync(item.Name);
                if (response.Value)
                    deleted++;
            }
            return deleted;
        }

        public Uri GetReadUrl(string key, TimeSpan validity)
        {
            var blob = _container.Value.GetBlobClient(key);
            return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validity));
        }
    }
}
