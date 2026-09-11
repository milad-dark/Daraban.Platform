using Ganss.Xss;

namespace Daraban.Platform.Common;

/// <summary>
/// Sanitizes user-supplied rich text before it is stored. The Angular client renders
/// several of these fields (KB article content, ticket task notes, ticket solutions)
/// with [innerHTML], so anything stored unsanitized is a stored-XSS vector. The
/// default allow-list keeps safe formatting markup and strips scripts, event
/// handlers and other active content.
/// </summary>
public static class HtmlInputSanitizer
{
    // HtmlSanitizer is thread-safe for concurrent Sanitize calls; one shared instance
    // avoids rebuilding the allow-list per request.
    private static readonly HtmlSanitizer Sanitizer = new();

    /// <summary>Returns sanitized HTML; null or empty input becomes string.Empty.</summary>
    public static string Sanitize(string? html)
        => string.IsNullOrEmpty(html) ? string.Empty : Sanitizer.Sanitize(html);
}
