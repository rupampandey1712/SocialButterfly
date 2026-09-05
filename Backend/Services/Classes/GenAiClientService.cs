using Grpc.Net.Client;
using SocialMediaWeb.Protos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class GenAiClientService : IGenAiClientService
    {
        private readonly GenAiService.GenAiServiceClient _client;

        public GenAiClientService(IConfiguration configuration)
        {
            var address = configuration["GenAI:GrpcAddress"]
                ?? throw new InvalidOperationException("GenAI:GrpcAddress is missing.");

            var channel = GrpcChannel.ForAddress(address);
            _client = new GenAiService.GenAiServiceClient(channel);
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken)
        {
            var response = await _client.GenerateTextAsync(
                new GenerateTextRequest { Prompt = prompt },
                cancellationToken: cancellationToken);

            return response.Text;
        }
    }
}
