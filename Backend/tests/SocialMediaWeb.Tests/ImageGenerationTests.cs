using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class ImageGenerationTests
{
    [Fact]
    public async Task Create_QueuesJobOnServiceBus_WithAspectSizeAndStyle()
    {
        await using var context = Seed();
        var bus = new RecordingBus();
        var service = new ImageGenerationService(context, bus, new FakeBlob());

        var job = await service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "a red fox in snow", Aspect = "portrait", Style = "watercolor" });

        Assert.Equal(GeneratedImageStatus.Queued, job.Status);
        Assert.Equal((768, 1152), (job.Width, job.Height));
        Assert.True(job.Seed > 0);
        var (queue, message) = Assert.Single(bus.Sent);
        Assert.Equal(ImageGenerationService.QueueName, queue);
        Assert.Equal(job.Id, ((ImageGenerationMessage)message!).JobId);
        Assert.EndsWith("textured paper", ImageGenerationService.FullPrompt(context.GeneratedImages.Single()));
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("   ")]
    [InlineData("naked child")]
    public async Task Create_RejectsTooShortOrProhibitedPrompts(string prompt)
    {
        await using var context = Seed();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, new GenerateImageRequestDto { Prompt = prompt }));
    }

    [Fact]
    public async Task Create_RejectsUnknownStyleAndAspect()
    {
        await using var context = Seed();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "a cat", Style = "<script>" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "a cat", Aspect = "9999x9999" }));
    }

    [Fact]
    public async Task Create_EnforcesActiveJobLimit()
    {
        await using var context = Seed();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());
        for (var i = 0; i < ImageGenerationService.MaxActiveJobsPerUser; i++)
        {
            await service.CreateAsync(1, new GenerateImageRequestDto { Prompt = $"mountain lake {i}" });
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "one more" }));
    }

    [Fact]
    public async Task Create_EnforcesDailyLimit()
    {
        await using var context = Seed();
        for (var i = 0; i < ImageGenerationService.MaxJobsPerDay; i++)
        {
            context.GeneratedImages.Add(new GeneratedImage { UserId = 1, Prompt = "x", Status = GeneratedImageStatus.Succeeded });
        }
        await context.SaveChangesAsync();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "a sunset" }));
    }

    [Fact]
    public async Task Get_OnlyReturnsOwnJobs()
    {
        await using var context = Seed();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());
        var job = await service.CreateAsync(1, new GenerateImageRequestDto { Prompt = "private dream" });

        Assert.NotNull(await service.GetAsync(job.Id, 1));
        Assert.Null(await service.GetAsync(job.Id, 2));
    }

    [Fact]
    public async Task Share_CreatesPostOnce_AndOnlyWhenReady()
    {
        await using var context = Seed();
        context.GeneratedImages.Add(new GeneratedImage { Id = 5, UserId = 1, Prompt = "neon city", Status = GeneratedImageStatus.Running });
        await context.SaveChangesAsync();
        var service = new ImageGenerationService(context, new RecordingBus(), new FakeBlob());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShareAsPostAsync(5, 1, null));

        var job = context.GeneratedImages.Single();
        job.Status = GeneratedImageStatus.Succeeded;
        job.ImagePath = "http://blob/ai-images/a.png";
        await context.SaveChangesAsync();

        var postId = await service.ShareAsPostAsync(5, 1, "My first AI image");
        Assert.Equal(postId, await service.ShareAsPostAsync(5, 1, "again"));
        var post = Assert.Single(context.Posts);
        Assert.Equal("http://blob/ai-images/a.png", post.ImagePath);
        Assert.Equal("My first AI image", post.Descriprion);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ShareAsPostAsync(5, 2, null));
    }

    [Fact]
    public void BuildWorkflow_FillsPromptSizeAndSeed_WithoutTouchingTemplate()
    {
        var comfy = NewComfy(new FakeComfy());

        var first = comfy.BuildWorkflow("a lighthouse", 768, 1152, 42);
        var second = comfy.BuildWorkflow("a forest", 1024, 1024, 7);

        Assert.Equal("a lighthouse", first["6"]!["inputs"]!["text"]!.GetValue<string>());
        Assert.Equal(768, first["5"]!["inputs"]!["width"]!.GetValue<int>());
        Assert.Equal(1152, first["5"]!["inputs"]!["height"]!.GetValue<int>());
        Assert.Equal(42, first["3"]!["inputs"]!["seed"]!.GetValue<long>());
        Assert.Equal("a forest", second["6"]!["inputs"]!["text"]!.GetValue<string>());
        Assert.Equal("flux1-schnell-fp8.safetensors", first["4"]!["inputs"]!["ckpt_name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Worker_RunsComfyWorkflow_AndStoresPngInBlob()
    {
        await using var context = Seed();
        context.GeneratedImages.Add(new GeneratedImage { Id = 9, UserId = 1, Prompt = "a koi pond", Style = "photo", Width = 1024, Height = 1024, Seed = 123 });
        await context.SaveChangesAsync();
        var comfyServer = new FakeComfy();
        var blob = new FakeBlob();
        var worker = new ImageGenerationWorker(context, NewComfy(comfyServer), blob, NullLogger<ImageGenerationWorker>.Instance);

        await worker.ProcessAsync(9, CancellationToken.None);

        var job = context.GeneratedImages.Single();
        Assert.Equal(GeneratedImageStatus.Succeeded, job.Status);
        Assert.Equal("ai-images/ai-9.png", job.ImagePath);
        Assert.Equal(FakeComfy.Png, blob.Bytes);
        Assert.Contains("a koi pond, professional photograph", comfyServer.QueuedPrompt);
        Assert.Contains("\"seed\":123", comfyServer.QueuedPrompt);

        // Redelivery of the same message does nothing.
        await worker.ProcessAsync(9, CancellationToken.None);
        Assert.Equal(1, comfyServer.QueueCalls);
    }

    [Fact]
    public async Task Worker_WhenComfyOffline_FailsWithFriendlyMessage()
    {
        await using var context = Seed();
        context.GeneratedImages.Add(new GeneratedImage { Id = 3, UserId = 1, Prompt = "anything", Width = 1024, Height = 1024, Seed = 1 });
        await context.SaveChangesAsync();
        var worker = new ImageGenerationWorker(context, NewComfy(new FakeComfy { Offline = true }), new FakeBlob(), NullLogger<ImageGenerationWorker>.Instance);

        await worker.ProcessAsync(3, CancellationToken.None);

        var job = context.GeneratedImages.Single();
        Assert.Equal(GeneratedImageStatus.Failed, job.Status);
        Assert.Contains("offline", job.Error);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public async Task Worker_WhenComfyReportsError_FailsJob()
    {
        await using var context = Seed();
        context.GeneratedImages.Add(new GeneratedImage { Id = 4, UserId = 1, Prompt = "anything", Width = 1024, Height = 1024, Seed = 1 });
        await context.SaveChangesAsync();
        var worker = new ImageGenerationWorker(context, NewComfy(new FakeComfy { FailJob = true }), new FakeBlob(), NullLogger<ImageGenerationWorker>.Instance);

        await worker.ProcessAsync(4, CancellationToken.None);

        Assert.Equal(GeneratedImageStatus.Failed, context.GeneratedImages.Single().Status);
    }

    private static ApplicationDbContext Seed()
    {
        var context = TestDbContextFactory.Create();
        context.Users.AddRange(new User { Id = 1, Name = "Ava" }, new User { Id = 2, Name = "Leo" });
        context.SaveChanges();
        return context;
    }

    private static ComfyUiClient NewComfy(FakeComfy server) =>
        new(new SingleClientFactory(server), new ConfigurationBuilder().Build(), NullLogger<ComfyUiClient>.Instance);

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false) { BaseAddress = new Uri("http://comfy.test/") };
    }

    /// <summary>Minimal stand-in for ComfyUI's /system_stats, /prompt, /history and /view endpoints.</summary>
    private sealed class FakeComfy : HttpMessageHandler
    {
        public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        public bool Offline { get; init; }
        public bool FailJob { get; init; }
        public string QueuedPrompt { get; private set; } = string.Empty;
        public int QueueCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline) throw new HttpRequestException("connection refused");
            var path = request.RequestUri!.AbsolutePath;

            if (path == "/system_stats") return Json("{}");
            if (path == "/prompt")
            {
                QueueCalls++;
                QueuedPrompt = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json("{\"prompt_id\":\"p1\"}");
            }
            if (path == "/history/p1")
            {
                return FailJob
                    ? Json("{\"p1\":{\"status\":{\"status_str\":\"error\"},\"outputs\":{}}}")
                    : Json("{\"p1\":{\"status\":{\"status_str\":\"success\"},\"outputs\":{\"9\":{\"images\":[{\"filename\":\"socialbutterfly_00001_.png\",\"subfolder\":\"\",\"type\":\"output\"}]}}}}");
            }
            if (path == "/view") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class RecordingBus : IServiceBusService
    {
        public List<(string Queue, object? Message)> Sent { get; } = new();
        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null)
        {
            Sent.Add((queueName, message));
            return Task.CompletedTask;
        }
        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null) => Task.FromResult(default(T));
    }

    private sealed class FakeBlob : IBlobStorageService
    {
        public byte[]? Bytes { get; private set; }
        public Task<string> UploadAsync(IFormFile file, string containerName) => Task.FromResult(file.FileName);
        public Task DeleteAsync(string blobUrl, string containerName) => Task.CompletedTask;
        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
        public Task<string> UploadBytesAsync(byte[] content, string fileName, string containerName)
        {
            Bytes = content;
            return Task.FromResult($"{containerName}/{fileName}");
        }
    }
}
