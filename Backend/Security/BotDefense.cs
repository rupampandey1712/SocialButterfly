using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace SocialMediaWeb.Security
{
    /// <summary>
    /// Risk scoring for sign-in endpoints, designed so genuine users are not affected:
    /// - only FAILED attempts count (a successful login resets that IP/account);
    /// - no account lockout (that would let attackers lock real users out) — instead
    ///   suspicious traffic must solve an increasingly expensive proof-of-work;
    /// - signals: failures per IP, failures per account, many different usernames from
    ///   one IP (credential stuffing), honeypot hits, and a global failure surge
    ///   (distributed botnet) which turns on a light challenge for everyone.
    /// State is in memory; with several API instances use a distributed cache instead.
    /// </summary>
    public sealed class BotDefense
    {
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan AttackWindow = TimeSpan.FromMinutes(5);

        public const int FreeFailuresPerIp = 3;
        public const int FreeFailuresPerAccount = 3;
        public const int DistinctIdentifiersPerIp = 5;
        public const int GlobalFailuresForAttackMode = 100;
        public const int BaseDifficulty = 16;
        public const int MaxDifficulty = 22;

        private readonly IMemoryCache _cache;
        private readonly ConcurrentQueue<DateTimeOffset> _recentFailures = new();
        private readonly TimeProvider _clock;

        public BotDefense(IMemoryCache cache, TimeProvider? clock = null)
        {
            _cache = cache;
            _clock = clock ?? TimeProvider.System;
        }

        private sealed class Tracker
        {
            public int Failures;
            public readonly HashSet<string> Identifiers = new();
            public bool Flagged;
        }

        /// <summary>Required proof-of-work difficulty in bits; 0 means no challenge.</summary>
        public int RequiredDifficulty(string ip, string? accountKey)
        {
            var ipTracker = Get("ip:" + ip);
            var accountTracker = accountKey == null ? null : Get("acct:" + accountKey);

            var score = 0;
            if (ipTracker != null)
            {
                score += Math.Max(ipTracker.Failures - FreeFailuresPerIp, 0);
                lock (ipTracker.Identifiers)
                {
                    if (ipTracker.Identifiers.Count > DistinctIdentifiersPerIp) score += 4;
                }
                if (ipTracker.Flagged) score += 10;
            }

            if (accountTracker != null)
            {
                score += Math.Max(accountTracker.Failures - FreeFailuresPerAccount, 0);
            }

            if (score > 0)
            {
                return Math.Min(BaseDifficulty + score, MaxDifficulty);
            }

            return IsUnderAttack() ? BaseDifficulty : 0;
        }

        public void RecordFailure(string ip, string? accountKey)
        {
            var ipTracker = GetOrCreate("ip:" + ip);
            Interlocked.Increment(ref ipTracker.Failures);
            if (accountKey != null)
            {
                lock (ipTracker.Identifiers)
                {
                    if (ipTracker.Identifiers.Count <= 50) ipTracker.Identifiers.Add(accountKey);
                }

                Interlocked.Increment(ref GetOrCreate("acct:" + accountKey).Failures);
            }

            var now = _clock.GetUtcNow();
            _recentFailures.Enqueue(now);
            TrimGlobal(now);
        }

        /// <summary>A genuine sign-in clears the history for that account and IP.</summary>
        public void RecordSuccess(string ip, string? accountKey)
        {
            if (accountKey != null) _cache.Remove("bot:acct:" + accountKey);
            var ipTracker = Get("ip:" + ip);
            if (ipTracker is { Flagged: false }) _cache.Remove("bot:ip:" + ip);
        }

        /// <summary>The hidden honeypot field was filled in: only automated form fillers do that.</summary>
        public void FlagBot(string ip)
        {
            GetOrCreate("ip:" + ip).Flagged = true;
        }

        public bool IsUnderAttack()
        {
            TrimGlobal(_clock.GetUtcNow());
            return _recentFailures.Count >= GlobalFailuresForAttackMode;
        }

        private void TrimGlobal(DateTimeOffset now)
        {
            while (_recentFailures.TryPeek(out var oldest) && now - oldest > AttackWindow)
            {
                _recentFailures.TryDequeue(out _);
            }
        }

        private Tracker? Get(string key) => _cache.TryGetValue("bot:" + key, out Tracker? tracker) ? tracker : null;

        private Tracker GetOrCreate(string key) =>
            _cache.GetOrCreate("bot:" + key, entry =>
            {
                entry.SlidingExpiration = Window;
                return new Tracker();
            })!;

        public static string? AccountKey(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;
            var value = identifier.Trim().TrimStart('@').ToLowerInvariant();
            return value.Length > 256 ? value[..256] : value;
        }
    }
}
