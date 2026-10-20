using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Validation and normalization for the three ways a member can identify
    /// themselves: email, phone number (E.164) and username.
    /// </summary>
    public static partial class IdentityRules
    {
        public const int MinimumPasswordLength = 8;

        [GeneratedRegex("^[a-z0-9._]{3,30}$")]
        private static partial Regex UsernamePattern();

        [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
        private static partial Regex PhonePattern();

        /// <summary>"@handle" is a username, so an email needs something before the '@'.</summary>
        public static bool LooksLikeEmail(string value) => value.Trim().IndexOf('@') > 0;

        public static bool LooksLikePhone(string value) =>
            Regex.IsMatch(value.Trim(), @"^\+?[\d\s\-().]{8,20}$");

        public static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var trimmed = email.Trim().ToLowerInvariant();
            try
            {
                var address = new MailAddress(trimmed);
                return address.Address == trimmed && trimmed.Contains('.', StringComparison.Ordinal) ? trimmed : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>Returns the E.164 form ("+919876543210"), or null when invalid. A country code is required.</summary>
        public static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var builder = new StringBuilder();
            foreach (var c in phone.Trim())
            {
                if (char.IsDigit(c) || (c == '+' && builder.Length == 0)) builder.Append(c);
                else if (c is not (' ' or '-' or '(' or ')' or '.')) return null;
            }

            var normalized = builder.ToString();
            if (normalized.StartsWith("00")) normalized = "+" + normalized[2..];
            return PhonePattern().IsMatch(normalized) ? normalized : null;
        }

        public static string? NormalizeUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            var normalized = username.Trim().TrimStart('@').ToLowerInvariant();
            return UsernamePattern().IsMatch(normalized) && !normalized.StartsWith('.') && !normalized.EndsWith('.')
                ? normalized
                : null;
        }

        /// <summary>Builds a username candidate from a display name or email local part.</summary>
        public static string UsernameSeed(string? name, string? email)
        {
            var source = !string.IsNullOrWhiteSpace(name) ? name : email?.Split('@')[0] ?? "member";
            var seed = new string(source.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : c is ' ' or '-' ? '.' : '\0')
                .Where(c => c != '\0')
                .ToArray())
                .Trim('.');
            seed = Regex.Replace(seed, @"\.{2,}", ".");
            if (seed.Length < 3) seed = $"member{seed}";
            return seed.Length > 24 ? seed[..24].TrimEnd('.') : seed;
        }

        public static string Mask(string target)
        {
            if (target.Contains('@'))
            {
                var parts = target.Split('@');
                var local = parts[0];
                var visible = local.Length <= 2 ? local[..1] : local[..2];
                return $"{visible}{new string('*', Math.Max(3, local.Length - visible.Length))}@{parts[1]}";
            }

            return target.Length <= 6
                ? target
                : $"{target[..3]}{new string('*', target.Length - 7)}{target[^4..]}";
        }
    }
}
