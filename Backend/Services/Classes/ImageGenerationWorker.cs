using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Functions-side processing of one queued image job: run the ComfyUI workflow,
    /// store the result in Blob Storage ("ai-images") and update the job row.
    /// Idempotent: a redelivered message for a finished job does nothing.
    /// </summary>
    public sealed class ImageGenerationWorker
    {
        private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(12);

        private readonly ApplicationDbContext _context;
        private readonly ComfyUiClient _comfy;
        private readonly Interfaces.IBlobStorageService _blobStorage;
        private readonly ILogger<ImageGenerationWorker> _logger;

        public ImageGenerationWorker(
            ApplicationDbContext context,
            ComfyUiClient comfy,
            Interfaces.IBlobStorageService blobStorage,
            ILogger<ImageGenerationWorker> logger)
        {
            _context = context;
            _comfy = comfy;
            _blobStorage = blobStorage;
            _logger = logger;
        }

        public async Task ProcessAsync(int jobId, CancellationToken cancellationToken)
        {
            var job = await _context.GeneratedImages.FirstOrDefaultAsync(g => g.Id == jobId, cancellationToken);
            if (job == null || job.Status is GeneratedImageStatus.Succeeded or GeneratedImageStatus.Failed)
            {
                return;
            }

            job.Status = GeneratedImageStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                if (!await _comfy.IsOnlineAsync(cancellationToken))
                {
                    throw new InvalidOperationException("The image generator is offline. Please try again later.");
                }

                var workflow = _comfy.BuildWorkflow(ImageGenerationService.FullPrompt(job), job.Width, job.Height, job.Seed);
                var png = await _comfy.GenerateAsync(workflow, GenerationTimeout, cancellationToken);

                job.ImagePath = await _blobStorage.UploadBytesAsync(png, $"ai-{job.Id}.png", ImageGenerationService.Container);
                job.Status = GeneratedImageStatus.Succeeded;
                _logger.LogInformation("Generated image {JobId} in {Seconds:0}s", job.Id, (DateTime.UtcNow - job.StartedAt.Value).TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // GPU jobs are expensive: fail once with a friendly message instead of retrying blindly.
                _logger.LogWarning(ex, "Image job {JobId} failed", job.Id);
                job.Status = GeneratedImageStatus.Failed;
                job.Error = ex is InvalidOperationException or TimeoutException
                    ? ex.Message
                    : "Something went wrong while creating this image.";
            }

            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
