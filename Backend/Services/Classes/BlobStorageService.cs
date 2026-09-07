using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<string> UploadAsync(IFormFile file, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            var extension = Path.GetExtension(file.FileName);
            var blobName = $"{fileName}_{DateTime.UtcNow.Ticks}{extension}";

            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            });

            _logger.LogInformation("Uploaded blob '{BlobName}' to container '{Container}'", blobName, containerName);
            return blobClient.Uri.ToString();
        }

        public async Task DeleteAsync(string blobUrl, string containerName)
        {
            var blobName = ExtractBlobName(blobUrl);
            if (string.IsNullOrEmpty(blobName))
            {
                _logger.LogWarning("Could not extract blob name from URL: {BlobUrl}", blobUrl);
                return;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var deleted = await containerClient.DeleteBlobIfExistsAsync(blobName);

            if (deleted)
            {
                _logger.LogInformation("Deleted blob '{BlobName}' from container '{Container}'", blobName, containerName);
            }
        }

        public string GetBlobUrl(string containerName, string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            return containerClient.GetBlobClient(blobName).Uri.ToString();
        }

        private static string ExtractBlobName(string blobUrl)
        {
            if (Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
            {
                // Blob URL format: http://host/container/blobname
                var segments = uri.Segments;
                return segments.Length >= 3 ? Uri.UnescapeDataString(segments[^1]) : Path.GetFileName(uri.LocalPath);
            }

            return Path.GetFileName(blobUrl);
        }
    }
}
