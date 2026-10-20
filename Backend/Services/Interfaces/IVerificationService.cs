using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IVerificationService
    {
        Task<SendCodeResponseDto> SendCodeAsync(SendCodeRequestDto request);

        /// <summary>
        /// Checks a code. Returns a signed-in session for the Login and Reset purposes,
        /// and null for Verify (the email/phone is simply marked as confirmed).
        /// </summary>
        Task<UserResponseDto?> ConfirmCodeAsync(ConfirmCodeRequestDto request);
    }

    /// <summary>
    /// Delivers a verification code. Locally this logs the message; in Azure, swap in
    /// an Azure Communication Services Email/SMS implementation.
    /// </summary>
    public interface IVerificationSender
    {
        Task SendAsync(VerificationCodeMessage message, CancellationToken cancellationToken = default);
    }
}
