using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Local stand-in for an email/SMS provider: writes the message to the Functions
    /// log ("docker compose logs chat-functions"). Replace with Azure Communication
    /// Services in a real deployment.
    /// </summary>
    public sealed class LoggingVerificationSender : IVerificationSender
    {
        private readonly ILogger<LoggingVerificationSender> _logger;

        public LoggingVerificationSender(ILogger<LoggingVerificationSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(VerificationCodeMessage message, CancellationToken cancellationToken = default)
        {
            var text = message.Purpose switch
            {
                VerificationPurposes.Login => $"{message.Code} is your SocialButterfly login code.",
                VerificationPurposes.Reset => $"{message.Code} is your SocialButterfly password reset code.",
                _ => $"{message.Code} is your SocialButterfly confirmation code."
            };

            _logger.LogInformation(
                "[{Channel}] To {Target}: {Text} (expires {ExpiresAt:u})",
                message.Channel == VerificationChannels.Email ? "EMAIL" : "SMS",
                IdentityRules.Mask(message.Target),
                text,
                message.ExpiresAt);

            return Task.CompletedTask;
        }
    }
}
