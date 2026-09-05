using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenAiController : ControllerBase
    {
        private readonly IGenAiClientService _genAiClientService;

        public GenAiController(IGenAiClientService genAiClientService)
        {
            _genAiClientService = genAiClientService;
        }

        [HttpPost("generate-text")]
        public async Task<ActionResult<GenAiGenerateTextResponseDto>> GenerateText(
            GenAiGenerateTextRequestDto request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest("Prompt is required.");
            }

            try
            {
                var text = await _genAiClientService.GenerateTextAsync(request.Prompt, cancellationToken);

                return Ok(new GenAiGenerateTextResponseDto
                {
                    Text = text
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                return BadRequest(ex.Status.Detail);
            }
            catch (RpcException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    $"Python GenAI service unavailable: {ex.Status.Detail}");
            }
        }
    }
}
