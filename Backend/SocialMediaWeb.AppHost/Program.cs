var builder = DistributedApplication.CreateBuilder(args);

// SQL Server
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sqlServer = builder.AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume("sqlserver-data");
var database = sqlServer.AddDatabase("SocialMedia");

// Azure Service Bus Emulator
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();
serviceBus.AddServiceBusQueue("post-notifications");
serviceBus.AddServiceBusQueue("friend-requests");
serviceBus.AddServiceBusQueue("chat-messages");

// Azure Storage (Azurite) Emulator
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Durable Functions owns queue-to-database delivery. The AppHost provides its
// Azure Storage host store and the same Service Bus connection used by the API.
builder.AddAzureFunctionsProject<Projects.SocialMediaWeb_ChatFunctions>("chat-functions")
    .WithHostStorage(storage)
    .WithEnvironment("ServiceBusConnection", serviceBus.Resource.ConnectionStringExpression)
    .WithEnvironment("ConnectionStrings__Con", database.Resource.ConnectionStringExpression)
    .WithEnvironment("ChatQueueName", "chat-messages")
    .WithEnvironment("DurableTaskHubName", "chatdelivery")
    .WaitFor(database)
    .WaitFor(serviceBus)
    .WaitFor(storage);

// API Project
builder.AddProject<Projects.SocialMediaWeb>("api")
    .WithExternalHttpEndpoints()
    .WithReference(database)
    .WithReference(serviceBus)
    .WithReference(blobs)
    .WaitFor(database)
    .WaitFor(serviceBus)
    .WaitFor(storage);

builder.Build().Run();
