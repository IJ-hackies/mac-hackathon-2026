using UnityEngine;

namespace Services.Leaderboards
{
    /// <summary>Local username rules shared by the game-over prompt and the dummy-data seed
    /// tool: length cap, allowed characters, and a lightweight profanity blocklist. The
    /// "has the player chosen a name through our prompt" flag lives here (PlayerPrefs-backed,
    /// same convention as LocalScoreRecord) rather than relying on
    /// AuthenticationService.PlayerName, because Unity Authentication assigns anonymous sessions
    /// an auto-generated default display name (e.g. "TestUser#12345") - checking that field for
    /// "empty" never actually gates the prompt.</summary>
    public static class UsernamePolicy
    {
        public const int MaxLength = 10;
        private const string HasChosenKey = "leaderboard.hasChosenName";

        // Deliberately small and blunt - a substring blocklist, not a full moderation system.
        // Good enough to stop casual/obvious abuse on a hackathon leaderboard.
        private static readonly string[] Blocklist =
        {
            "fuck", "shit", "bitch", "cunt", "asshole", "dick", "pussy", "whore", "slut",
            "nigger", "nigga", "faggot", "retard", "rape", "nazi", "hitler", "kike", "spic",
            "chink", "tranny", "porn", "sex"
        };

        public static bool HasChosenName => PlayerPrefs.GetInt(HasChosenKey, 0) == 1;

        public static void MarkNameChosen()
        {
            PlayerPrefs.SetInt(HasChosenKey, 1);
            PlayerPrefs.Save();
        }

        public static bool TryValidate(string raw, out string sanitized, out string error)
        {
            sanitized = (raw ?? string.Empty).Trim();

            if (sanitized.Length == 0)
            {
                error = "Enter a username.";
                return false;
            }

            if (sanitized.Length > MaxLength)
            {
                error = $"Username must be {MaxLength} characters or fewer.";
                return false;
            }

            foreach (char c in sanitized)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != ' ')
                {
                    error = "Letters, numbers, spaces, and _ only.";
                    return false;
                }
            }

            string lowered = sanitized.ToLowerInvariant();
            foreach (string word in Blocklist)
            {
                if (lowered.Contains(word))
                {
                    error = "That username isn't allowed.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
