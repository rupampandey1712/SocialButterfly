namespace SocialMediaWeb.Security
{
    /// <summary>
    /// Builds SQL LIKE patterns from user input. EF Core already parameterizes the value
    /// (no SQL injection), but %, _ and [ would still act as wildcards, letting a user
    /// craft expensive or misleading searches. They are escaped with SQL Server brackets.
    /// </summary>
    public static class SearchText
    {
        public const int MaximumLength = 100;

        public static string Contains(string term)
        {
            var trimmed = term.Trim();
            if (trimmed.Length > MaximumLength)
            {
                trimmed = trimmed[..MaximumLength];
            }

            var escaped = trimmed
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
            return $"%{escaped}%";
        }
    }
}
