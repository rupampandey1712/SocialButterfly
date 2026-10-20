using Microsoft.AspNetCore.Http;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class MediaTests
{
    [Fact]
    public async Task SongPost_IsStoredInMusicContainer_WithTitleFromFileName()
    {
        await using var context = Seed();
        var blob = new RecordingBlobStorage();
        var posts = new SocialMedia(context, blob, new NotificationPublisher(new NoOpServiceBus()));

        var post = await posts.CreatePostAsync(new PostCreateDto
        {
            UserId = 1,
            ImageFile = File("Summer_Nights.mp3", "audio/mpeg"),
            MusicArtist = "The Band"
        });

        Assert.Equal("music", blob.Uploads.Single().Container);
        Assert.Equal("Summer Nights", post.MusicTitle);
        Assert.Equal("The Band", post.MusicArtist);
        Assert.Null(post.MusicPath);
    }

    [Fact]
    public async Task PhotoPost_WithMusic_UploadsBoth()
    {
        await using var context = Seed();
        var blob = new RecordingBlobStorage();
        var posts = new SocialMedia(context, blob, new NotificationPublisher(new NoOpServiceBus()));

        var post = await posts.CreatePostAsync(new PostCreateDto
        {
            UserId = 1,
            ImageFile = File("beach.jpg", "image/jpeg"),
            MusicFile = File("waves.m4a", "audio/mp4"),
            MusicTitle = "Waves"
        });

        Assert.Equal(new[] { "post-images", "music" }, blob.Uploads.Select(u => u.Container));
        Assert.Equal("waves.m4a", post.MusicPath);
        Assert.Equal("Waves", post.MusicTitle);
    }

    [Fact]
    public async Task MusicOnTextPost_IsRejected()
    {
        await using var context = Seed();
        var posts = new SocialMedia(context, new RecordingBlobStorage(), new NotificationPublisher(new NoOpServiceBus()));

        await Assert.ThrowsAsync<ArgumentException>(() => posts.CreatePostAsync(new PostCreateDto
        {
            UserId = 1,
            Description = "Just words",
            MusicFile = File("song.mp3", "audio/mpeg")
        }));
    }

    [Theory]
    [InlineData("payload.exe", "image/png")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("song.mp3", "image/jpeg")]
    public async Task MismatchedOrUnsupportedFiles_AreRejected(string fileName, string contentType)
    {
        await using var context = Seed();
        var posts = new SocialMedia(context, new RecordingBlobStorage(), new NotificationPublisher(new NoOpServiceBus()));

        await Assert.ThrowsAsync<ArgumentException>(() => posts.CreatePostAsync(new PostCreateDto
        {
            UserId = 1,
            ImageFile = File(fileName, contentType)
        }));
    }

    [Fact]
    public async Task Story_WithMusic_StoresTrack()
    {
        await using var context = Seed();
        var blob = new RecordingBlobStorage();
        var stories = new StoryService(context, blob);

        var story = await stories.CreateAsync(1, File("city.mp4", "video/mp4"), "Night drive",
            File("drive.mp3", "audio/mpeg"), "Night Drive", null);

        Assert.Equal("drive.mp3", story.MusicPath);
        Assert.Equal("Night Drive", story.MusicTitle);
        Assert.Equal(new[] { "stories", "music" }, blob.Uploads.Select(u => u.Container));
    }

    [Fact]
    public async Task Story_WithNonAudioMusic_IsRejected()
    {
        await using var context = Seed();
        var stories = new StoryService(context, new RecordingBlobStorage());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            stories.CreateAsync(1, File("a.jpg", "image/jpeg"), null, File("b.jpg", "image/jpeg")));
    }

    private static ApplicationDbContext Seed()
    {
        var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, Name = "Ava" });
        context.SaveChanges();
        return context;
    }

    private static IFormFile File(string name, string contentType)
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        return new FormFile(stream, 0, stream.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private sealed class RecordingBlobStorage : IBlobStorageService
    {
        public List<(string Name, string Container)> Uploads { get; } = new();

        public Task<string> UploadAsync(IFormFile file, string containerName)
        {
            Uploads.Add((file.FileName, containerName));
            return Task.FromResult(file.FileName);
        }

        public Task DeleteAsync(string blobUrl, string containerName) => Task.CompletedTask;
        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
    }

    private sealed class NoOpServiceBus : IServiceBusService
    {
        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null) => Task.CompletedTask;
        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null) => Task.FromResult(default(T));
    }
}
