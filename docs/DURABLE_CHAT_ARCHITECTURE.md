# Durable chat implementation

## What was added

This implementation provides direct messages between authenticated users. It uses a
durable asynchronous path rather than inserting a message during the HTTP request:

```text
React chat page
    |
    | POST /api/chat (JWT)
    v
ASP.NET Core API -- Service Bus `chat-messages` queue
                                    |
                                    | ServiceBusTrigger
                                    v
                    Azure Durable Function starter
                                    |
                                    | Durable orchestration state in Azure Storage
                                    v
                    SQL persistence activity --> ChatMessages table
                                    |
                                    v
                  React polls GET /api/chat/with/{userId}
```

The Function project uses the .NET isolated worker model and Durable Functions. This
is the supported direction for .NET Functions; Microsoft documents `DurableTaskClient`
for starting workflows and `TaskOrchestrationContext` for running them
([Durable Functions isolated overview](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-dotnet-isolated-overview)).

## Components and responsibility

| Component | Location | Responsibility |
| --- | --- | --- |
| React page | `Frontend/src/pages/chat/Chat.jsx` | Select a member, send a message, show queued state, poll delivered messages, mark received messages as read. |
| Chat API | `Backend/Controllers/ChatController.cs` | Enforces JWT identity and accepts/reads/marks messages. The sender is always taken from the token, never from the request body. |
| Queue publisher | `Backend/Services/Classes/ChatService.cs` | Validates active users and publishes a `ChatQueueMessage` with a stable `MessageId`. |
| Service Bus sender | `Backend/Services/Classes/ServiceBusService.cs` | Serializes payloads and sets the broker `MessageId`, enabling broker duplicate detection. |
| Durable Function | `Backend/SocialMediaWeb.ChatFunctions/ChatDeliveryFunctions.cs` | Starts an orchestration per message, retries storage work, and persists one message. |
| SQL store | `Backend/Services/Classes/ChatMessageStore.cs` | Applies the database-side idempotency guard and writes `ChatMessages`. |
| Aspire AppHost | `Backend/SocialMediaWeb.AppHost/Program.cs` | Runs the API, Service Bus emulator, Azurite, SQL Server, and Function together in local distributed development. |
| Docker Compose | `docker-compose.yml` | Runs the same messaging/storage topology without requiring the Aspire dashboard. |

## Backend API

All chat routes require a bearer token. The `NameIdentifier` claim generated at login is
used as the sender/reader identifier.

### Send a message

```http
POST /api/chat
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "receiverId": 42,
  "content": "Hi, are you free later?",
  "clientMessageId": "49f71b3a-29f6-4f93-ae92-5eb8f805682d"
}
```

The endpoint returns `202 Accepted` with the same `messageId`. A client should reuse
the same `clientMessageId` if its network request times out. Both users must exist,
be active, be different users, and content is trimmed and limited to 2,000 characters.

### Read a conversation

```http
GET /api/chat/with/42?limit=100
Authorization: Bearer <access-token>
```

Only the caller's own two-party conversation is returned. The API bounds `limit` to
1–100. The page then calls `PUT /api/chat/with/42/read` to mark only messages received
by the current user as read.

### Queue publication

`ChatService.QueueMessageAsync` makes the message identifier stable and passes it to
the Service Bus sender:

```csharp
var messageId = request.ClientMessageId ?? Guid.NewGuid();
await _serviceBus.SendMessageAsync(
    QueueName,
    new ChatQueueMessage(messageId, senderId, request.ReceiverId, content, DateTime.UtcNow),
    messageId.ToString("N"));
```

`ServiceBusService` writes that value to `ServiceBusMessage.MessageId`. The Docker
emulator configuration enables a ten-minute duplicate detection window for
`chat-messages`. Broker-level duplicate detection is helpful for immediate retries;
the database remains the permanent idempotency boundary.

## Durable Function and delivery guarantee

The Function is in `Backend/SocialMediaWeb.ChatFunctions`, is added to the solution,
and is registered in the Aspire AppHost with `AddAzureFunctionsProject`.

### 1. Service Bus trigger checkpoints the work

```csharp
[Function(nameof(StartChatDelivery))]
public async Task StartChatDelivery(
    [ServiceBusTrigger("%ChatQueueName%", Connection = "ServiceBusConnection")] string payload,
    [DurableClient] DurableTaskClient client,
    CancellationToken cancellationToken)
{
    var message = JsonSerializer.Deserialize<ChatQueueMessage>(payload)
        ?? throw new JsonException("The chat queue message had no payload.");

    await client.ScheduleNewOrchestrationInstanceAsync(
        new TaskName(nameof(ChatDeliveryOrchestrator)),
        message,
        new StartOrchestrationOptions { InstanceId = $"chat-{message.MessageId:N}" },
        cancellationToken);
}
```

The Function runtime only completes the Service Bus trigger after this method succeeds.
Therefore, if the Function host fails before Durable Task accepts the orchestration,
Service Bus redelivers the message. The stable durable instance ID makes repeated
delivery converge on the same workflow. Microsoft documents that an active
orchestration must have a unique instance ID
([Durable Task singleton orchestration guidance](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-singletons)).

### 2. Durable orchestration retries the activity

```csharp
var retryOptions = new TaskOptions(
    new TaskRetryOptions(
        new RetryPolicy(maxNumberOfAttempts: 5, firstRetryInterval: TimeSpan.FromSeconds(3))));

return await context.CallActivityAsync<string>(
    new TaskName(nameof(PersistChatMessageActivity)), message, retryOptions);
```

Orchestration history and retry timers are persisted in Azure Storage through
`AzureWebJobsStorage`; they survive a Function restart. The activity runs separately
and is allowed five delivery attempts. This follows the isolated worker retry model,
which uses `TaskOptions` with `CallActivityAsync`
([Microsoft migration guidance](https://learn.microsoft.com/en-us/azure/azure-functions/durable-functions/durable-functions-migrate)).

### 3. SQL persistence is idempotent

`ChatMessages.MessageId` has a unique index. The `ChatMessageStore` first checks it
and treats a duplicate-key race as success:

```csharp
if (await _context.ChatMessages.AnyAsync(x => x.MessageId == message.MessageId, cancellationToken))
    return;

_context.ChatMessages.Add(new ChatMessage { /* payload fields */ });
await _context.SaveChangesAsync(cancellationToken);
```

This is essential: Service Bus and Durable Task both use at-least-once delivery.
If an activity commits SQL and then the host fails before reporting completion, the
activity can run again without creating a duplicate message.

### Failure behaviour

| Failure point | Result |
| --- | --- |
| HTTP client retries POST | Same client message ID is broker-deduplicated in the configured time window; SQL still prevents a later duplicate. |
| API fails before Service Bus accepts send | Request fails; no message is presented as queued. |
| Service Bus trigger host fails before durable schedule | Trigger lock expires and Service Bus redelivers. |
| Durable Function or storage restarts after schedule | Azure Storage replays/resumes the saved orchestration state. |
| SQL unavailable | Activity uses durable retry policy; a terminally failed orchestration remains inspectable rather than silently discarding the message. |
| SQL commits but activity completion is lost | Activity repeats, sees `MessageId`, and succeeds without a second insert. |

## Data stores

### SQL Server — user-facing chat history

EF Core migration `20260907162659_AddChatMessages` creates `ChatMessages` with sender,
receiver, message body, send/delivery timestamps, read state, a unique `MessageId`, and
conversation indexes. Apply it before deploying:

```powershell
cd Backend
dotnet ef database update
```

`Database__ApplyMigrations=true` in Docker Compose applies migrations on API startup
for local Compose development only. Production deployments should execute the migration
as a controlled release step instead of relying on automatic startup migration.

### Azure Service Bus — asynchronous command queue

The queue is named `chat-messages`. Aspire declares it in
`SocialMediaWeb.AppHost/Program.cs`; Compose declaratively creates it from
`infra/servicebus/config.json`. The local emulator supports AMQP and JSON-defined
entities, but its entities are intentionally not persistent across emulator restarts
([Service Bus emulator overview](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)).

In Azure, provision a Standard or Premium namespace, create the same queue, enable
duplicate detection if desired, and supply its connection string as `servicebus` to the
API and `ServiceBusConnection` to the Function. Prefer managed identity and RBAC over
connection strings in a production AppHost deployment.

### Azure Storage / Azurite — Durable Task state and blobs

`AzureWebJobsStorage` is the Durable Functions data store: it persists orchestration
history, internal queues, leases, and retry timers. The existing `blobs` connection
continues to hold profile and post media. Compose uses a single Azurite container for
both needs and persists its data in the `azurite-data` volume.

For Azure deployment, set `AzureWebJobsStorage` to a dedicated Storage account and
keep the Function's storage account separate from disposable media storage when your
operational requirements call for it.

## Aspire development setup

The AppHost now uses the Azure Functions hosting integration and starts the Functions
project with:

```csharp
builder.AddAzureFunctionsProject<Projects.SocialMediaWeb_ChatFunctions>("chat-functions")
    .WithHostStorage(storage)
    .WithEnvironment("ServiceBusConnection", serviceBus.Resource.ConnectionStringExpression)
    .WithEnvironment("ConnectionStrings__Con", database.Resource.ConnectionStringExpression)
    .WithEnvironment("ChatQueueName", "chat-messages")
    .WithEnvironment("DurableTaskHubName", "chatdelivery");
```

`WithHostStorage(storage)` gives the Functions runtime its Azure Storage host store.
The explicit environment values map the Service Bus trigger and SQL activity to the
same emulated resources as the API. Aspire's Functions integration is designed to add
Functions projects with `AddAzureFunctionsProject`, rather than normal `AddProject`
([Azure Functions with Aspire](https://learn.microsoft.com/en-au/azure/azure-functions/dotnet-aspire-integration)).

Run it with:

```powershell
cd Backend
dotnet run --project SocialMediaWeb.AppHost
```

For a non-Aspire local Function host, copy
`SocialMediaWeb.ChatFunctions/local.settings.json.example` to `local.settings.json`,
set its SQL connection, start Azurite and a Service Bus emulator, then run the Function
with Azure Functions Core Tools.

## Docker Compose setup

`docker-compose.yml` now starts five core services:

1. `sqlserver` for application data.
2. `azurite` for Azure Blob, Queue, and Table APIs plus Durable Task state.
3. `servicebus` using Microsoft's Service Bus emulator image and the versioned queue configuration.
4. `app`, the combined React/ASP.NET Core image.
5. `chat-functions`, an isolated .NET 10 Azure Functions image.

Start the stack:

```powershell
docker compose up --build
```

The Function image is built from
`Backend/SocialMediaWeb.ChatFunctions/Dockerfile` and uses the official
`mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0` runtime image.
The image family and .NET 10 isolated worker are supported by Functions v4
([Functions isolated-worker support](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)).

The Service Bus emulator's configuration must be present before startup; it does not
apply entity changes live. `infra/servicebus/config.json` is therefore mounted read-only
into the container, following Microsoft's emulator container guidance
([local emulator setup](https://learn.microsoft.com/azure/service-bus-messaging/test-locally-with-service-bus-emulator)).

## Frontend behaviour

The Messages icon in the top bar and Chats item in the sidebar navigate to `/chat`.
The page uses the existing JWT from session storage and:

- loads members from the existing account endpoint;
- writes an optimistic **Queued** bubble after the API returns `202`;
- polls the conversation every three seconds;
- replaces the queued bubble when durable SQL delivery is visible;
- acknowledges unread inbound messages with the read endpoint;
- restores the draft and reports an error if queue submission fails.

The page deliberately polls instead of claiming browser push. A later SignalR or Azure
Web PubSub addition can subscribe to the same persisted message state without changing
the durable delivery contract.

## Validation

The implementation includes `ChatServiceTests`. Its principal smoke path queues a
message, simulates delivery twice, confirms exactly one persisted message, reads it,
and marks it read. The test is intentionally local and does not require a cloud account.

Recommended verification commands:

```powershell
cd Backend
dotnet build SocialMediaWeb.ChatFunctions/SocialMediaWeb.ChatFunctions.csproj
dotnet build SocialMediaWeb.AppHost/SocialMediaWeb.AppHost.csproj
dotnet test tests/SocialMediaWeb.Tests/SocialMediaWeb.Tests.csproj

cd ../Frontend
npm run build

cd ..
docker compose config
```

For an end-to-end manual check, start either Aspire or Compose, log in as two users,
send a message from one session, and confirm it appears in the other session after the
queue/function cycle. Inspect the Function console for orchestration and activity logs;
inspect the Service Bus emulator's exposed health/management port for local broker state.

## Operational notes

- Keep `clientMessageId` stable across client retries.
- Monitor dead-letter messages and failed Durable Task instances; they represent work
  that needs investigation after automatic retry is exhausted.
- Use a production-safe SQL account, Key Vault, and managed identities. The Compose
  emulator secrets are for local development only.
- Review authorization policies before permitting arbitrary member-to-member messages;
  the current implementation permits any active account to contact any other active account.
- Message content is persisted as text. Add encryption, retention, moderation, blocking,
  attachments, and audit policies before treating the feature as a production messenger.
