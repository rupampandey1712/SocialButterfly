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
    })
    .Build();

await host.RunAsync();
