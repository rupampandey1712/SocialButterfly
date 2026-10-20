using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Talks to a ComfyUI server over its HTTP API: queue a workflow (POST /prompt), wait for
    /// it (GET /history/{id}) and download the image (GET /view).
    ///
    /// The workflow is a template in ComfyUI "API format". By default it is the bundled
    /// Flux-schnell text-to-image graph (ComfyUI/flux-schnell-txt2img.json); point
    /// Comfy:WorkflowPath at another exported workflow and set Comfy:Nodes:* to its node ids.
    /// </summary>
    public sealed class ComfyUiClient
    {
        public const string HttpClientName = "comfyui";

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ComfyUiClient> _logger;
        private readonly Lazy<JsonObject> _template;

        public ComfyUiClient(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<ComfyUiClient> logger)
        {
            _http = httpFactory.CreateClient(HttpClientName);
            _configuration = configuration;
            _logger = logger;
            _template = new Lazy<JsonObject>(LoadTemplate);
        }

        public static void Configure(HttpClient client, IConfiguration configuration)
        {
            client.BaseAddress = new Uri((configuration["Comfy:BaseUrl"] ?? "http://127.0.0.1:8188").TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        }

        private string Node(string role, string fallback) => _configuration[$"Comfy:Nodes:{role}"] ?? fallback;

        /// <summary>Fills the template with the prompt, size and seed.</summary>
        public JsonObject BuildWorkflow(string prompt, int width, int height, long seed)
        {
            var workflow = (JsonObject)_template.Value.DeepClone();
            Inputs(workflow, Node("Prompt", "6"))["text"] = prompt;
            var latent = Inputs(workflow, Node("Latent", "5"));
            latent["width"] = width;
            latent["height"] = height;
            Inputs(workflow, Node("Sampler", "3"))["seed"] = seed;
            return workflow;
        }

        public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var response = await _http.GetAsync("system_stats", timeout.Token);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>Queues the workflow, waits for it and returns the PNG bytes.</summary>
        public async Task<byte[]> GenerateAsync(JsonObject workflow, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var queueResponse = await _http.PostAsJsonAsync("prompt", new { prompt = workflow, client_id = "socialbutterfly" }, cancellationToken);
            var queueBody = await queueResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!queueResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("ComfyUI rejected the workflow: {Body}", queueBody.Length > 500 ? queueBody[..500] : queueBody);
                throw new InvalidOperationException("The image generator rejected this request.");
            }

            var promptId = JsonNode.Parse(queueBody)?["prompt_id"]?.GetValue<string>()
                ?? throw new InvalidOperationException("ComfyUI did not return a prompt id.");

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var history = await _http.GetFromJsonAsync<JsonObject>($"history/{promptId}", cancellationToken);
                if (history?[promptId] is JsonObject entry)
                {
                    if (entry["status"]?["status_str"]?.GetValue<string>() == "error")
                    {
                        _logger.LogWarning("ComfyUI job {PromptId} failed: {Status}", promptId, entry["status"]?.ToJsonString());
                        throw new InvalidOperationException("The image generator failed to create this image.");
                    }

                    foreach (var output in entry["outputs"]?.AsObject() ?? new JsonObject())
                    {
                        if (output.Value?["images"] is JsonArray images && images.Count > 0 && images[0] is JsonObject image)
                        {
                            var query = $"filename={Uri.EscapeDataString(image["filename"]!.GetValue<string>())}" +
                                        $"&subfolder={Uri.EscapeDataString(image["subfolder"]?.GetValue<string>() ?? "")}" +
                                        $"&type={Uri.EscapeDataString(image["type"]?.GetValue<string>() ?? "output")}";
                            return await _http.GetByteArrayAsync($"view?{query}", cancellationToken);
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            throw new TimeoutException("The image took too long to generate.");
        }

        private JsonObject LoadTemplate()
        {
            var path = _configuration["Comfy:WorkflowPath"];
            string json;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            else
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("SocialMediaWeb.ComfyUI.flux-schnell-txt2img.json")
                    ?? throw new InvalidOperationException("The bundled ComfyUI workflow is missing.");
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("The ComfyUI workflow must be a JSON object in API format.");
        }

        private static JsonObject Inputs(JsonObject workflow, string nodeId) =>
            workflow[nodeId]?["inputs"] as JsonObject
            ?? throw new InvalidOperationException($"ComfyUI workflow has no node '{nodeId}' with inputs (check Comfy:Nodes settings).");
    }
}
