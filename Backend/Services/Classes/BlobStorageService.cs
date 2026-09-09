using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly string? _publicBaseUrl;

        public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger, IConfiguration configuration)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _publicBaseUrl = configuration["Blob:PublicBaseUrl"];
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
            return ToPublicUrl(blobClient.Uri);
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
            return ToPublicUrl(containerClient.GetBlobClient(blobName).Uri);
        }

        // In Docker the blob endpoint host (e.g. "azurite") is internal-only;
        // return a base URL that is reachable by the browser when configured.
        public string? ToPublicUrl(string? blobUrl)
        {
            if (string.IsNullOrWhiteSpace(blobUrl))
                return blobUrl;

            if (Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
            {
                return ToPublicUrl(uri);
            }

            return blobUrl;
        }

        private string ToPublicUrl(Uri blobUri)
        {
            if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            {
                var baseUri = new Uri(_publicBaseUrl);
                var builder = new UriBuilder(blobUri)
                {
                    Scheme = baseUri.Scheme,
                    Host = baseUri.Host,
                    Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
                };
                return builder.Uri.ToString();
            }

            if (string.Equals(blobUri.Host, "azurite", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(blobUri)
                {
                    Host = "localhost"
                };
                return builder.Uri.ToString();
            }

            return blobUri.ToString();
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
