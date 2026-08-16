using System.Linq;
using System.Text;

namespace Services.Leaderboards
{
    /// <summary>Pure validation/filtering logic for leaderboard usernames - no UI dependencies.</summary>
    public static class UsernamePolicy
    {
        public const int MaxLength = 12;

        // Small local denylist covering common English profanity/slurs. Matched case-insensitively
        // as a substring so simple concatenation tricks ("f-u-c-k" aside) are still caught.
        private static readonly string[] BannedSubstrings =
        {
            "fuck", "shit", "bitch", "cunt", "asshole", "dick", "pussy", "nigger", "nigga",
            "faggot", "retard", "whore", "slut", "rape", "nazi",
        };

        public static bool IsAllowedCharacter(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var builder = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (IsAllowedCharacter(c)) builder.Append(c);
            }
            string trimmed = builder.ToString();
            return trimmed.Length > MaxLength ? trimmed.Substring(0, MaxLength) : trimmed;
        }

        public static bool Validate(string candidate, out string reason)
        {
            string trimmed = candidate?.Trim() ?? string.Empty;

            if (trimmed.Length == 0)
            {
                reason = "Enter a username.";
                return false;
            }
            if (trimmed.Length > MaxLength)
            {
                reason = $"Username must be {MaxLength} characters or fewer.";
                return false;
            }
            if (trimmed.Any(c => !IsAllowedCharacter(c)))
            {
                reason = "Only letters, numbers, - and _ are allowed.";
                return false;
            }
            string lowered = trimmed.ToLowerInvariant();
            if (BannedSubstrings.Any(lowered.Contains))
            {
                reason = "That username isn't allowed.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
