using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SocialMediaWeb.Security
{
    /// <summary>
    /// Stateless, self-hosted proof-of-work challenge (no CAPTCHA, no third party).
    /// The server signs a random challenge with a difficulty; the browser must find a
    /// nonce so that SHA-256(challenge + ":" + nonce) starts with that many zero bits.
    /// One human attempt costs milliseconds; a bot making thousands of attempts pays
    /// that cost every time, and the difficulty rises as an IP or account looks abusive.
    /// </summary>
    public sealed class ProofOfWork
    {
        public const string HeaderName = "X-Bot-Proof";
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        private readonly byte[] _key;
        private readonly IMemoryCache _usedChallenges;

        public ProofOfWork(IConfiguration configuration, IMemoryCache cache)
        {
            // Derived from the JWT secret so every API instance agrees without extra config.
            var secret = configuration["Security:PowSecret"] ?? configuration["JWT:Secret"] ?? string.Empty;
            _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("socialbutterfly-pow-v1"));
            _usedChallenges = cache;
        }

        private sealed record Payload(string S, int D, long E, string Sc);

        public string Issue(int difficulty, string scope)
        {
            var payload = new Payload(
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                difficulty,
                DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds(),
                scope);
            var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
            return $"{body}.{Sign(body)}";
        }

        /// <summary>Checks a "challenge:nonce" proof. Each challenge can be used once.</summary>
        public bool Verify(string? proof, int requiredDifficulty, string scope)
        {
            if (string.IsNullOrWhiteSpace(proof) || proof.Length > 512)
            {
                return false;
            }

            var separator = proof.LastIndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            var challenge = proof[..separator];
            var nonce = proof[(separator + 1)..];
            var parts = challenge.Split('.');
            if (parts.Length != 2 || nonce.Length is 0 or > 20 || !nonce.All(char.IsAsciiDigit))
            {
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(parts[0])), Encoding.ASCII.GetBytes(parts[1])))
            {
                return false;
            }

            Payload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(FromBase64Url(parts[0]));
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                return false;
            }

            if (payload == null ||
                payload.Sc != scope ||
                payload.D < requiredDifficulty ||
                DateTimeOffset.FromUnixTimeSeconds(payload.E) < DateTimeOffset.UtcNow ||
                LeadingZeroBits(SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge}:{nonce}"))) < payload.D)
            {
                return false;
            }

            // Single use: a solved challenge cannot be replayed for more attempts.
            var cacheKey = "pow-used:" + parts[1];
            if (_usedChallenges.TryGetValue(cacheKey, out _))
            {
                return false;
            }

            _usedChallenges.Set(cacheKey, true, Lifetime);
            return true;
        }

        public static int LeadingZeroBits(byte[] hash)
        {
            var bits = 0;
            foreach (var b in hash)
            {
                if (b == 0)
                {
                    bits += 8;
                    continue;
                }

                bits += System.Numerics.BitOperations.LeadingZeroCount((uint)b) - 24;
                break;
            }

            return bits;
        }

        private string Sign(string body) => Base64Url(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(body)));

        private static string Base64Url(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            return Convert.FromBase64String(padded);
        }
    }
}
