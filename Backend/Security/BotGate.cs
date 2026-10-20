using Microsoft.AspNetCore.Mvc;

namespace SocialMediaWeb.Security
{
    /// <summary>
    /// Front door for sign-in style endpoints: asks <see cref="BotDefense"/> how risky the
    /// request is and, when needed, demands a valid proof-of-work (HTTP 428 + challenge).
    /// The frontend solves it in a Web Worker and retries automatically.
    /// </summary>
    public sealed class BotGate
    {
        private readonly BotDefense _defense;
        private readonly ProofOfWork _pow;

        public BotGate(BotDefense defense, ProofOfWork pow)
        {
            _defense = defense;
            _pow = pow;
        }

        public BotDefense Defense => _defense;

        public static string ClientIp(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        /// <summary>Returns a 428 result when a (valid) proof-of-work is required; otherwise null.</summary>
        public ActionResult? Check(HttpContext context, string scope, string? accountKey)
        {
            var difficulty = _defense.RequiredDifficulty(ClientIp(context), accountKey);
            if (difficulty == 0)
            {
                return null;
            }

            var proof = context.Request.Headers[ProofOfWork.HeaderName].ToString();
            if (_pow.Verify(proof, difficulty, scope))
            {
                return null;
            }

            return new ObjectResult(new
            {
                error = "challenge_required",
                message = "Verifying you're human…",
                challenge = _pow.Issue(difficulty, scope),
                difficulty
            })
            { StatusCode = StatusCodes.Status428PreconditionRequired };
        }

        /// <summary>
        /// Honeypot: real users never see or fill the hidden "website" field. Bots that
        /// auto-fill every input get flagged (and receive a normal-looking failure).
        /// </summary>
        public bool IsHoneypotTripped(HttpContext context, string? honeypotValue)
        {
            if (string.IsNullOrEmpty(honeypotValue))
            {
                return false;
            }

            _defense.FlagBot(ClientIp(context));
            return true;
        }
    }
}
