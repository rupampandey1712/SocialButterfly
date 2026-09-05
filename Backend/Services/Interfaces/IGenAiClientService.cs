namespace SocialMediaWeb.Services.Interfaces
{
    public interface IGenAiClientService
    {
        Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken);
    }
}
