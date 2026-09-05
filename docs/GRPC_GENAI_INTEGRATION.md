# gRPC GenAI Integration Guide

This document explains how gRPC works at a basic level and how it should be used in this project to connect the ASP.NET Core backend with Python-based GenAI functionality that calls NVIDIA NIM.

## Current Status

A basic gRPC implementation has been added to the codebase.

The current project structure is:

```text
SocialMediaWeb/
+-- Backend/     ASP.NET Core Web API
+-- Frontend/    React app
+-- docs/        Project documentation
```

The implemented GenAI structure is:

```text
React Frontend
   |
   | REST / JSON
   v
ASP.NET Core Backend
   |
   | gRPC
   v
Python GenAI Service
   |
   | HTTP OpenAI-compatible API
   v
NVIDIA NIM
```

The React frontend should keep calling the ASP.NET Core API. The Python service should not be exposed directly to the browser.

## What gRPC Is

gRPC is a backend-to-backend communication framework. Instead of manually creating REST endpoints and JSON payloads, both services share a `.proto` contract file.

That `.proto` file defines:

- The service name
- The callable methods
- The request message shape
- The response message shape

Code is then generated from the `.proto` file for both .NET and Python. This gives both sides strongly typed clients and servers.

## Why Use gRPC Here

gRPC is useful for this project because:

- The main backend is .NET, but GenAI code is easier to build in Python.
- Python dependencies stay isolated from the ASP.NET Core app.
- The .NET backend can call Python through a typed client.
- Streaming responses can be added later for token-by-token AI output.
- It avoids running Python scripts directly from C#, which is harder to monitor and scale.

## What NVIDIA NIM Does

NVIDIA NIM provides model inference endpoints. For GenAI tasks, NIM is normally called over HTTP using OpenAI-compatible APIs.

So gRPC is not replacing the NIM API. Instead:

- .NET calls Python through gRPC.
- Python calls NVIDIA NIM through HTTP.

Example NIM base URLs:

```text
Hosted NVIDIA endpoint:
https://integrate.api.nvidia.com/v1

Local NIM container:
http://localhost:8000/v1
```

## Added Files

The implementation added these files:

```text
Backend/
+-- Protos/
|   +-- genai.proto
+-- Services/
|   +-- Interfaces/
|   |   +-- IGenAiClientService.cs
|   +-- Classes/
|       +-- GenAiClientService.cs
+-- Controllers/
|   +-- GenAiController.cs
+-- GenAI/
    +-- requirements.txt
    +-- server.py
    +-- .env.example
    +-- genai_pb2.py
    +-- genai_pb2_grpc.py
```

## Basic Proto Contract

Added `Backend/Protos/genai.proto`:

```proto
syntax = "proto3";

option csharp_namespace = "SocialMediaWeb.Protos";

package genai;

service GenAiService {
  rpc GenerateText (GenerateTextRequest) returns (GenerateTextResponse);
}

message GenerateTextRequest {
  string prompt = 1;
}

message GenerateTextResponse {
  string text = 1;
}
```

This contract means:

- `GenAiService` is the gRPC service.
- `GenerateText` is the method .NET can call.
- .NET sends a `prompt`.
- Python returns generated `text`.

## Python Service Setup

Added `Backend/GenAI/requirements.txt`:

```text
grpcio
grpcio-tools
openai
python-dotenv
```

Install dependencies:

```powershell
cd Backend\GenAI
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
```

The Python gRPC files are already present:

```text
Backend/GenAI/genai_pb2.py
Backend/GenAI/genai_pb2_grpc.py
```

If the proto contract changes, regenerate them with:

```powershell
python -m grpc_tools.protoc `
  -I ..\Protos `
  --python_out=. `
  --grpc_python_out=. `
  ..\Protos\genai.proto
```

## Python gRPC Server Example

Added `Backend/GenAI/server.py`:

```python
from concurrent import futures
import os

import grpc
from openai import OpenAI

import genai_pb2
import genai_pb2_grpc


class GenAiService(genai_pb2_grpc.GenAiServiceServicer):
    def __init__(self):
        self.client = OpenAI(
            base_url=os.environ["NIM_BASE_URL"],
            api_key=os.environ["NVIDIA_API_KEY"],
        )
        self.model = os.environ["NIM_MODEL"]

    def GenerateText(self, request, context):
        completion = self.client.chat.completions.create(
            model=self.model,
            messages=[{"role": "user", "content": request.prompt}],
            max_tokens=250,
        )

        text = completion.choices[0].message.content or ""
        return genai_pb2.GenerateTextResponse(text=text)


def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    genai_pb2_grpc.add_GenAiServiceServicer_to_server(GenAiService(), server)
    server.add_insecure_port("[::]:50051")
    server.start()
    print("Python GenAI gRPC service running on port 50051")
    server.wait_for_termination()


if __name__ == "__main__":
    serve()
```

Run the Python service:

```powershell
$env:NVIDIA_API_KEY="your-nvidia-api-key"
$env:NIM_BASE_URL="https://integrate.api.nvidia.com/v1"
$env:NIM_MODEL="your-nim-model-id"
python server.py
```

For a local NIM container, use:

```powershell
$env:NIM_BASE_URL="http://localhost:8000/v1"
```

## .NET Backend Setup

The gRPC package references were added to `Backend/SocialMediaWeb.csproj`.

If you need to add them again manually, run:

```powershell
cd Backend
dotnet add package Grpc.Net.Client
dotnet add package Google.Protobuf
dotnet add package Grpc.Tools
```

`Backend/SocialMediaWeb.csproj` includes:

```xml
<ItemGroup>
  <Protobuf Include="Protos\genai.proto" GrpcServices="Client" />
</ItemGroup>
```

This tells .NET to generate the gRPC client classes from `genai.proto`.

## .NET Client Service

Added `Backend/Services/Interfaces/IGenAiClientService.cs`:

```csharp
namespace SocialMediaWeb.Services.Interfaces;

public interface IGenAiClientService
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken);
}
```

Added `Backend/Services/Classes/GenAiClientService.cs`:

```csharp
using Grpc.Net.Client;
using SocialMediaWeb.Protos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes;

public sealed class GenAiClientService : IGenAiClientService
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
```

## Register The .NET Service

In `Backend/Program.cs`, the client service is registered with dependency injection:

```csharp
builder.Services.AddSingleton<IGenAiClientService, GenAiClientService>();
```

Add it near the existing service registrations:

```csharp
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISocialMediaPost, SocialMedia>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IGenAiClientService, GenAiClientService>();
```

## .NET Controller Example

Added `Backend/Controllers/GenAiController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class GenAiController : ControllerBase
{
    private readonly IGenAiClientService _genAiClientService;

    public GenAiController(IGenAiClientService genAiClientService)
    {
        _genAiClientService = genAiClientService;
    }

    [HttpPost("generate-text")]
    public async Task<ActionResult<GenerateTextResponseDto>> GenerateText(
        GenerateTextRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var text = await _genAiClientService.GenerateTextAsync(request.Prompt, cancellationToken);
        return Ok(new GenerateTextResponseDto(text));
    }
}

public sealed record GenerateTextRequestDto(string Prompt);

public sealed record GenerateTextResponseDto(string Text);
```

This lets the frontend call:

```text
POST /api/GenAi/generate-text
```

with:

```json
{
  "prompt": "Write a social media caption for a new profile picture."
}
```

## Configuration

Added this to `Backend/appsettings.Development.json`:

```json
{
  "GenAI": {
    "GrpcAddress": "http://localhost:50051"
  }
}
```

Keep these as environment variables for Python:

```powershell
$env:NVIDIA_API_KEY="your-nvidia-api-key"
$env:NIM_BASE_URL="https://integrate.api.nvidia.com/v1"
$env:NIM_MODEL="your-nim-model-id"
```

Do not commit real API keys.

## Request Flow

1. User sends a prompt from React.
2. React calls the ASP.NET Core endpoint.
3. ASP.NET validates authentication and request data.
4. ASP.NET calls the Python GenAI service through gRPC.
5. Python calls NVIDIA NIM using the OpenAI-compatible API.
6. NIM returns model output to Python.
7. Python returns the generated text to ASP.NET through gRPC.
8. ASP.NET returns JSON to React.

## Running Locally

Terminal 1, start Python service:

```powershell
cd Backend\GenAI
.\.venv\Scripts\activate
$env:NVIDIA_API_KEY="your-nvidia-api-key"
$env:NIM_BASE_URL="https://integrate.api.nvidia.com/v1"
$env:NIM_MODEL="your-nim-model-id"
python server.py
```

Terminal 2, start ASP.NET Core backend:

```powershell
cd Backend
dotnet run
```

Terminal 3, start React frontend:

```powershell
cd Frontend
npm start
```

## Production Notes

For production:

- Use TLS for gRPC instead of insecure local HTTP.
- Store API keys in a secret manager or environment variables.
- Add timeouts to the .NET gRPC client.
- Add retry policies only for safe transient failures.
- Add logging around Python service failures.
- Add health checks for both Python gRPC and NVIDIA NIM readiness.
- Run the Python service as a separate process, container, or Kubernetes deployment.

## When To Avoid gRPC

Do not use gRPC if:

- The Python functionality is very small and rarely used.
- You do not need typed backend-to-backend communication.
- You want the simplest possible deployment.

In that case, a Python FastAPI HTTP service may be simpler. For this project, gRPC is still a good fit if GenAI features will grow.
