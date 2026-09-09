namespace SocialMediaWeb.Services.Interfaces
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a file to the specified blob container and returns the blob URL.
        /// </summary>
        Task<string> UploadAsync(IFormFile file, string containerName);

        /// <summary>
        /// Deletes a blob by its URL from the specified container.
        /// </summary>
        Task DeleteAsync(string blobUrl, string containerName);

        /// <summary>
        /// Gets the full URL for a blob in the specified container.
        /// </summary>
        string GetBlobUrl(string containerName, string blobName);

        /// <summary>
        /// Converts an internal blob URL (e.g. Docker azurite endpoint) to a publicly accessible URL for clients.
        /// </summary>
        string? ToPublicUrl(string? blobUrl);
    }
}
