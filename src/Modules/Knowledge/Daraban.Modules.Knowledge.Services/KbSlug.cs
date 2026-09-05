using System.Globalization;
using System.Text;

namespace Daraban.Modules.Knowledge.Services;

/// <summary>
/// Turns a category name into a URL-safe slug. Lives here rather than in Common because it is
/// the Knowledge module's own URL convention, not a platform-wide rule.
/// </summary>
internal static class KbSlug
{
    private const int MaxLength = 200;

    public static string From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Decompose accents (é -> e + combining acute) then drop the combining marks, so
        // "Réseau" becomes "reseau" instead of "rseau".
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].Trim('-') : slug;
    }
}
