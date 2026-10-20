using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

var host = new HostBuilder()
    .ConfigureAppConfiguration(configuration => configuration.AddEnvironmentVariables())
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetConnectionString("Con")
            ?? context.Configuration["ConnectionStrings:Con"]
            ?? throw new InvalidOperationException("Connection string 'Con' is missing.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IChatMessageStore, ChatMessageStore>();
        services.AddScoped<INotificationStore, NotificationStore>();

        // Story cleanup deletes blobs from the same storage account the host uses (Azurite locally).
        var storageConnection = context.Configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is missing.");
        services.AddSingleton(new BlobServiceClient(storageConnection));
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IStoryService, StoryService>();
        services.AddSingleton<IVerificationSender, LoggingVerificationSender>();

        // AI image studio: ComfyUI runs on the host GPU (Comfy:BaseUrl), results go to Blob Storage.
        services.AddHttpClient(ComfyUiClient.HttpClientName, client => ComfyUiClient.Configure(client, context.Configuration));
        services.AddSingleton<ComfyUiClient>();
        services.AddScoped<ImageGenerationWorker>();
    })
    .Build();

await host.RunAsync();
