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

            var blobName = SafeBlobName(file.FileName);

            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    // Derived from the (already validated) extension, never trusted from the client,
                    // so an upload can't be served as HTML/SVG/script.
                    ContentType = ContentTypeFor(blobName),
                    CacheControl = "public, max-age=31536000, immutable"
                }
            });

            _logger.LogInformation("Uploaded blob '{BlobName}' to container '{Container}'", blobName, containerName);
            return ToPublicUrl(blobClient.Uri);
        }

        public async Task<string> UploadBytesAsync(byte[] content, string fileName, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobName = SafeBlobName(fileName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(new BinaryData(content), new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentTypeFor(blobName),
                    CacheControl = "public, max-age=31536000, immutable"
                }
            });

            _logger.LogInformation("Uploaded generated blob '{BlobName}' to container '{Container}'", blobName, containerName);
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

        private static readonly Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider ContentTypes = new();

        /// <summary>
        /// Random, collision-free blob name that keeps a short, sanitized hint of the original
        /// file name. Client file names can contain path separators, control or Unicode tricks.
        /// </summary>
        public static string SafeBlobName(string? originalFileName)
        {
            var name = Path.GetFileName(originalFileName ?? string.Empty);
            var extension = Path.GetExtension(name).ToLowerInvariant();
            if (extension.Length > 6 || !extension.Skip(1).All(char.IsAsciiLetterOrDigit))
            {
                extension = string.Empty;
            }

            var stem = new string(Path.GetFileNameWithoutExtension(name)
                .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_')
                .ToArray()).Trim('_');
            if (stem.Length > 40) stem = stem[..40];
            if (stem.Length == 0) stem = "file";

            return $"{stem}_{Guid.NewGuid():N}{extension}";
        }

        public static string ContentTypeFor(string blobName) =>
            ContentTypes.TryGetContentType(blobName, out var type) ? type : "application/octet-stream";

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
