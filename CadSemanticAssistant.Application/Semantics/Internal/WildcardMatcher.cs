using System.Text.RegularExpressions;

namespace CadSemanticAssistant.Application.Semantics.Internal;

internal static class WildcardMatcher
{
    public static bool IsMatch(string value, string pattern)
    {
        string regularExpression =
            "^" +
            Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") +
            "$";

        return Regex.IsMatch(value, regularExpression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
