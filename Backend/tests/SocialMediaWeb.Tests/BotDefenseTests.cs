using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using SocialMediaWeb.Security;

namespace SocialMediaWeb.Tests;

public class BotDefenseTests
{
    private static BotDefense NewDefense(ManualClock? clock = null) =>
        new(new MemoryCache(new MemoryCacheOptions()), clock);

    [Fact]
    public void GenuineUser_WithAFewTypos_IsNeverChallenged()
    {
        var defense = NewDefense();
        for (var i = 0; i < BotDefense.FreeFailuresPerAccount; i++)
        {
            defense.RecordFailure("10.0.0.1", "ava.martin");
        }

        Assert.Equal(0, defense.RequiredDifficulty("10.0.0.1", "ava.martin"));
    }

    [Fact]
    public void RepeatedFailures_EscalateDifficulty_UpToTheCap()
    {
        var defense = NewDefense();
        var previous = 0;
        for (var i = 0; i < 30; i++)
        {
            defense.RecordFailure("10.0.0.2", "victim");
            var difficulty = defense.RequiredDifficulty("10.0.0.2", "victim");
            Assert.True(difficulty >= previous);
            previous = difficulty;
        }

        Assert.Equal(BotDefense.MaxDifficulty, previous);
    }

    [Fact]
    public void AttackOnOneAccount_FromManyIps_StillChallengesThatAccount()
    {
        var defense = NewDefense();
        for (var i = 0; i < 10; i++)
        {
            defense.RecordFailure($"203.0.113.{i}", "victim");   // distributed: one try per IP
        }

        Assert.True(defense.RequiredDifficulty("198.51.100.7", "victim") > 0);
        Assert.Equal(0, defense.RequiredDifficulty("198.51.100.7", "someone.else"));
    }

    [Fact]
    public void SuccessfulLogin_ResetsHistory()
    {
        var defense = NewDefense();
        for (var i = 0; i < 6; i++) defense.RecordFailure("10.0.0.3", "leo.park");
        Assert.True(defense.RequiredDifficulty("10.0.0.3", "leo.park") > 0);

        defense.RecordSuccess("10.0.0.3", "leo.park");

        Assert.Equal(0, defense.RequiredDifficulty("10.0.0.3", "leo.park"));
    }

    [Fact]
    public void ManyDifferentUsernamesFromOneIp_IsTreatedAsCredentialStuffing()
    {
        var defense = NewDefense();
        for (var i = 0; i < BotDefense.DistinctIdentifiersPerIp + 1; i++)
        {
            defense.RecordFailure("10.0.0.4", $"user{i}");
        }

        Assert.True(defense.RequiredDifficulty("10.0.0.4", "brand.new.target") >= BotDefense.BaseDifficulty);
    }

    [Fact]
    public void HoneypotHit_FlagsTheIp()
    {
        var defense = NewDefense();
        defense.FlagBot("10.0.0.5");

        Assert.Equal(BotDefense.MaxDifficulty, defense.RequiredDifficulty("10.0.0.5", null));
        defense.RecordSuccess("10.0.0.5", null);
        Assert.Equal(BotDefense.MaxDifficulty, defense.RequiredDifficulty("10.0.0.5", null));
    }

    [Fact]
    public void GlobalFailureSurge_TurnsOnLightChallengeForEveryone_ThenExpires()
    {
        var clock = new ManualClock();
        var defense = NewDefense(clock);
        for (var i = 0; i < BotDefense.GlobalFailuresForAttackMode; i++)
        {
            defense.RecordFailure($"198.18.{i / 250}.{i % 250}", $"random{i}");
        }

        Assert.True(defense.IsUnderAttack());
        Assert.Equal(BotDefense.BaseDifficulty, defense.RequiredDifficulty("192.0.2.10", "innocent"));

        clock.Advance(BotDefense.AttackWindow + TimeSpan.FromSeconds(1));
        Assert.False(defense.IsUnderAttack());
        Assert.Equal(0, defense.RequiredDifficulty("192.0.2.10", "innocent"));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

public class ProofOfWorkTests
{
    private static ProofOfWork NewPow() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JWT:Secret"] = "test-secret-with-enough-length-1234567890" }).Build(),
        new MemoryCache(new MemoryCacheOptions()));

    private static string Solve(string challenge, int difficulty)
    {
        for (long nonce = 0; ; nonce++)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge}:{nonce}"));
            if (ProofOfWork.LeadingZeroBits(hash) >= difficulty) return $"{challenge}:{nonce}";
        }
    }

    [Fact]
    public void SolvedChallenge_IsAccepted_OnlyOnce()
    {
        var pow = NewPow();
        var proof = Solve(pow.Issue(10, "login"), 10);

        Assert.True(pow.Verify(proof, 10, "login"));
        Assert.False(pow.Verify(proof, 10, "login"));  // replay
    }

    [Fact]
    public void WrongNonce_TamperedChallenge_OrWrongScope_AreRejected()
    {
        var pow = NewPow();
        var challenge = pow.Issue(12, "login");
        var proof = Solve(challenge, 12);

        var unsolved = Enumerable.Range(0, 100).Select(n => $"{challenge}:{n}").First(p => LeadingZerosOf(p) < 12);
        Assert.False(pow.Verify(unsolved, 12, "login"));
        Assert.False(pow.Verify(proof, 12, "register"));
        var tampered = "A" + proof[1..];
        Assert.False(pow.Verify(tampered, 12, "login"));
        Assert.False(pow.Verify("garbage", 12, "login"));
        Assert.False(pow.Verify(null, 12, "login"));
    }

    [Fact]
    public void EasierChallengeThanRequired_IsRejected()
    {
        var pow = NewPow();
        var proof = Solve(pow.Issue(8, "login"), 8);

        Assert.False(pow.Verify(proof, 16, "login"));
    }

    [Fact]
    public void ChallengeFromAnotherServerSecret_IsRejected()
    {
        var other = new ProofOfWork(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JWT:Secret"] = "a-completely-different-secret-value-000" }).Build(),
            new MemoryCache(new MemoryCacheOptions()));
        var forged = Solve(other.Issue(8, "login"), 8);

        Assert.False(NewPow().Verify(forged, 8, "login"));
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x80 }, 16)]
    [InlineData(new byte[] { 0x0F }, 4)]
    [InlineData(new byte[] { 0x01 }, 7)]
    [InlineData(new byte[] { 0xFF }, 0)]
    public void LeadingZeroBits_IsCorrect(byte[] hash, int expected)
    {
        Assert.Equal(expected, ProofOfWork.LeadingZeroBits(hash));
    }

    private static int LeadingZerosOf(string proof) =>
        ProofOfWork.LeadingZeroBits(SHA256.HashData(Encoding.UTF8.GetBytes(proof)));
}
