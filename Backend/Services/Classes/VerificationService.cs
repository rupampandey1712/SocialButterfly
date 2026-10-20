using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Issues and checks 6-digit one-time codes for email/phone verification,
    /// passwordless login and password reset. Delivery is asynchronous: the code is
    /// queued on Service Bus ("verification-codes") and the Functions worker hands
    /// it to an <see cref="IVerificationSender"/> (email/SMS provider).
    /// </summary>
    public class VerificationService : IVerificationService
    {
        public const string QueueName = "verification-codes";
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
        private const int MaximumAttempts = 5;
        private const int MaximumCodesPerHour = 5;

        private readonly ApplicationDbContext _context;
        private readonly IServiceBusService _serviceBus;
        private readonly IUserService _users;
        private readonly bool _exposeDevCodes;
        private readonly ILogger<VerificationService>? _logger;

        public VerificationService(
            ApplicationDbContext context,
            IServiceBusService serviceBus,
            IUserService users,
            IHostEnvironment environment,
            IConfiguration configuration,
            ILogger<VerificationService>? logger = null)
        {
            _context = context;
            _serviceBus = serviceBus;
            _users = users;
            _logger = logger;
            _exposeDevCodes = environment.IsDevelopment() && configuration.GetValue("Verification:ExposeDevCodes", true);
        }

        public async Task<SendCodeResponseDto> SendCodeAsync(SendCodeRequestDto request)
        {
            var purpose = request.Purpose ?? VerificationPurposes.Verify;
            if (!VerificationPurposes.IsValid(purpose))
            {
                throw new ArgumentException("Unknown verification purpose.");
            }

            var (target, channel) = NormalizeTarget(request.Target);
            var user = await FindOwnerAsync(target, channel);
            if (user == null)
            {
                if (purpose == VerificationPurposes.Verify)
                {
                    throw new KeyNotFoundException("We couldn't find an account with that contact.");
                }

                // Login/Reset: answer exactly as if a code was sent, so the endpoint can't be
                // used to discover which emails and phone numbers have accounts.
                return new SendCodeResponseDto
                {
                    Channel = channel,
                    Destination = IdentityRules.Mask(target),
                    ExpiresAt = DateTime.UtcNow.Add(CodeLifetime)
                };
            }

            if (purpose == VerificationPurposes.Verify && IsConfirmed(user, channel))
            {
                throw new InvalidOperationException("This contact is already verified.");
            }

            var now = DateTime.UtcNow;
            var recent = await _context.VerificationCodes
                .Where(c => c.Target == target && c.Purpose == purpose && c.CreatedAt > now.AddHours(-1))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            if (recent.Count > 0 && now - recent[0].CreatedAt < ResendCooldown)
            {
                var wait = (int)Math.Ceiling((ResendCooldown - (now - recent[0].CreatedAt)).TotalSeconds);
                throw new InvalidOperationException($"Please wait {wait} seconds before requesting another code.");
            }

            if (recent.Count >= MaximumCodesPerHour)
            {
                throw new InvalidOperationException("Too many codes requested. Please try again later.");
            }

            // A new code supersedes any still-active one for the same purpose.
            foreach (var active in recent.Where(c => c.ConsumedAt == null))
            {
                active.ConsumedAt = now;
            }

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var entry = new VerificationCode
            {
                UserId = user.Id,
                Target = target,
                Channel = channel,
                Purpose = purpose,
                CodeHash = Hash(target, code),
                CreatedAt = now,
                ExpiresAt = now.Add(CodeLifetime)
            };
            _context.VerificationCodes.Add(entry);
            await _context.SaveChangesAsync();

            try
            {
                var message = new VerificationCodeMessage(Guid.NewGuid(), channel, target, purpose, code, entry.ExpiresAt);
                await _serviceBus.SendMessageAsync(QueueName, message, message.EventId.ToString("N"));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not queue {Channel} verification code for delivery", channel);
                if (!_exposeDevCodes)
                {
                    throw new InvalidOperationException("We couldn't send your code right now. Please try again.");
                }
            }

            return new SendCodeResponseDto
            {
                Channel = channel,
                Destination = IdentityRules.Mask(target),
                ExpiresAt = entry.ExpiresAt,
                DevCode = _exposeDevCodes ? code : null
            };
        }

        public async Task<UserResponseDto?> ConfirmCodeAsync(ConfirmCodeRequestDto request)
        {
            var purpose = request.Purpose ?? VerificationPurposes.Verify;
            if (!VerificationPurposes.IsValid(purpose))
            {
                throw new ArgumentException("Unknown verification purpose.");
            }

            var (target, channel) = NormalizeTarget(request.Target);
            var code = (request.Code ?? string.Empty).Trim();
            var now = DateTime.UtcNow;

            var entry = await _context.VerificationCodes
                .Include(c => c.User)
                .Where(c => c.Target == target && c.Purpose == purpose && c.ConsumedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (entry == null || entry.ExpiresAt <= now || entry.User == null || entry.User.IsDeleted)
            {
                throw new InvalidOperationException("That code has expired. Request a new one.");
            }

            if (entry.Attempts >= MaximumAttempts)
            {
                throw new InvalidOperationException("Too many incorrect attempts. Request a new code.");
            }

            if (code.Length != 6 || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(entry.CodeHash), Encoding.ASCII.GetBytes(Hash(target, code))))
            {
                entry.Attempts++;
                await _context.SaveChangesAsync();
                throw new UnauthorizedAccessException("That code isn't valid. Check it and try again.");
            }

            var user = entry.User;

            if (purpose == VerificationPurposes.Reset)
            {
                UserService.SetPassword(user, request.NewPassword);
            }

            entry.ConsumedAt = now;

            // Proving access to the inbox/phone verifies it, whatever the purpose.
            if (channel == VerificationChannels.Email) user.EmailConfirmed = true;
            else user.PhoneConfirmed = true;

            await _context.SaveChangesAsync();

            return purpose == VerificationPurposes.Verify ? null : _users.ToLoginResponse(user);
        }

        private async Task<User?> FindOwnerAsync(string target, string channel)
        {
            return channel == VerificationChannels.Email
                ? await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == target && !u.IsDeleted)
                : await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == target && !u.IsDeleted);
        }

        private static bool IsConfirmed(User user, string channel) =>
            channel == VerificationChannels.Email ? user.EmailConfirmed : user.PhoneConfirmed;

        private static (string Target, string Channel) NormalizeTarget(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new ArgumentException("Enter your email address or mobile number.");
            }

            if (IdentityRules.LooksLikeEmail(raw))
            {
                return (IdentityRules.NormalizeEmail(raw) ?? throw new ArgumentException("Enter a valid email address."),
                    VerificationChannels.Email);
            }

            return (IdentityRules.NormalizePhone(raw)
                    ?? throw new ArgumentException("Enter a valid mobile number with country code, e.g. +91 98765 43210."),
                VerificationChannels.Sms);
        }

        private static string Hash(string target, string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{target}:{code}"));
            return Convert.ToHexString(bytes);
        }
    }
}
