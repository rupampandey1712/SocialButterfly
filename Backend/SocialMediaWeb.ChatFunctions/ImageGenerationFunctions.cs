using Microsoft.Azure.Functions.Worker;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using System.Text.Json;

namespace SocialMediaWeb.ChatFunctions;

/// <summary>
/// AI image studio worker: takes a job id from the "image-generation" queue, runs the
/// ComfyUI workflow on the local GPU and stores the PNG in Blob Storage ("ai-images").
/// Long-running, so host.json raises the function timeout and Service Bus lock renewal.
/// </summary>
public sealed class ImageGenerationFunctions
{
    private readonly ImageGenerationWorker _worker;

    public ImageGenerationFunctions(ImageGenerationWorker worker)
    {
        _worker = worker;
    }

    [Function(nameof(GenerateImage))]
    public async Task GenerateImage(
        [ServiceBusTrigger("%ImageGenerationQueueName%", Connection = "ServiceBusConnection")] string payload,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ImageGenerationMessage>(payload)
            ?? throw new JsonException("The image generation message had no payload.");

        await _worker.ProcessAsync(message.JobId, cancellationToken);
    }
}
