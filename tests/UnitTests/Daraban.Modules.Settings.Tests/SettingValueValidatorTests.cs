using Daraban.Modules.Settings.Services;
using Xunit;

namespace Daraban.Modules.Settings.Tests;

/// <summary>
/// Per-key validation rules (Task 7.4). The printable-ASCII and no-CR/LF rules are the
/// security floor: setting values flow into SMTP envelopes, directory filters and HTML,
/// so control characters must be rejected at the boundary. Pinned by tests deliberately.
/// </summary>
public class SettingValueValidatorTests
{
    // ---- Cross-cutting rules -----------------------------------------------------------

    [Fact]
    public void Rejects_Unknown_Keys()
    {
        var error = SettingValueValidator.Validate("not.a.real_key", "anything");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.KEY_UNKNOWN", error.Value.Code);
    }

    [Fact]
    public void Rejects_Values_Over_The_Length_Cap()
    {
        var error = SettingValueValidator.Validate("branding.application_name", new string('a', 501));

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.VALUE_TOO_LONG", error.Value.Code);
    }

    [Theory]
    [InlineData("line1\r\nBcc: attacker@example.com")] // SMTP header injection attempt
    [InlineData("tab\there")]
    [InlineData("null\0byte")]
    [InlineData("\u00e9 accent")] // non-ASCII -- rejected by the printable-ASCII floor
    public void Rejects_Control_And_NonAscii_Characters(string value)
    {
        var error = SettingValueValidator.Validate("branding.application_name", value);

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_CHARACTERS", error.Value.Code);
    }

    [Theory]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    public void Rejects_Leading_Or_Trailing_Whitespace(string value)
    {
        var error = SettingValueValidator.Validate("branding.application_name", value);

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_CHARACTERS", error.Value.Code);
    }

    // ---- Typed values --------------------------------------------------------------------

    [Theory]
    [InlineData("587")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Accepts_Integer_Values(string value)
    {
        Assert.Null(SettingValueValidator.Validate("email.smtp_port", value));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("58.7")]
    [InlineData("")]
    public void Rejects_Non_Integer_Values(string value)
    {
        var error = SettingValueValidator.Validate("email.smtp_port", value);

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_INT", error.Value.Code);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("False")]
    public void Accepts_Booleans_Case_Insensitively(string value)
    {
        Assert.Null(SettingValueValidator.Validate("security.mfa_enforced", value));
    }

    [Fact]
    public void Rejects_Boolean_Gibberish()
    {
        var error = SettingValueValidator.Validate("security.mfa_enforced", "yes");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_BOOLEAN", error.Value.Code);
    }

    [Theory]
    [InlineData("08:00")]
    [InlineData("23:59")]
    [InlineData("00:00")]
    public void Accepts_Valid_Times(string value)
    {
        Assert.Null(SettingValueValidator.Validate("time.work_start", value));
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("7:30")]
    [InlineData("ab:cd")]
    [InlineData("0800")]
    [InlineData("12:60")]
    public void Rejects_Invalid_Times(string value)
    {
        var error = SettingValueValidator.Validate("time.work_start", value);

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_TIME", error.Value.Code);
    }

    // ---- Per-key string rules --------------------------------------------------------------

    [Theory]
    [InlineData("none")]
    [InlineData("STARTTLS")]
    [InlineData("ssl")]
    public void Accepts_The_Three_TLS_Modes(string value)
    {
        Assert.Null(SettingValueValidator.Validate("email.smtp_tls", value));
    }

    [Fact]
    public void Rejects_Unknown_TLS_Mode()
    {
        var error = SettingValueValidator.Validate("email.smtp_tls", "tls1.3");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_TLS_MODE", error.Value.Code);
    }

    [Theory]
    [InlineData("it@example.com")]
    [InlineData("first.last+tag@sub.example.org")]
    public void Accepts_Valid_From_Addresses(string value)
    {
        Assert.Null(SettingValueValidator.Validate("email.from_address", value));
    }

    [Fact]
    public void Rejects_Invalid_From_Address()
    {
        var error = SettingValueValidator.Validate("email.from_address", "not-an-email");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_EMAIL", error.Value.Code);
    }

    [Fact]
    public void Accepts_An_Https_Logo_Url()
    {
        Assert.Null(SettingValueValidator.Validate("branding.logo_url", "https://cdn.example.com/logo.png"));
    }

    [Fact]
    public void Rejects_A_Relative_Logo_Url()
    {
        var error = SettingValueValidator.Validate("branding.logo_url", "/assets/logo.png");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_URL", error.Value.Code);
    }

    [Theory]
    [InlineData("#1976d2")]
    [InlineData("#FFFFFF")]
    public void Accepts_Hex_Colors(string value)
    {
        Assert.Null(SettingValueValidator.Validate("branding.primary_color", value));
    }

    [Theory]
    [InlineData("1976d2")]
    [InlineData("#1976d")]
    [InlineData("#gggggg")]
    [InlineData("red")]
    public void Rejects_Non_Hex_Colors(string value)
    {
        var error = SettingValueValidator.Validate("branding.primary_color", value);

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_COLOR", error.Value.Code);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Europe/Berlin")]
    [InlineData("Asia/Tehran")]
    public void Accepts_Valid_IANA_Timezones(string value)
    {
        Assert.Null(SettingValueValidator.Validate("time.default_timezone", value));
    }

    [Fact]
    public void Rejects_An_Unknown_Timezone()
    {
        var error = SettingValueValidator.Validate("time.default_timezone", "Mars/Olympus_Mons");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_TIMEZONE", error.Value.Code);
    }

    [Theory]
    [InlineData("yyyy-MM-dd")]
    [InlineData("dd/MM/yyyy")]
    public void Accepts_Plausible_Date_Formats(string value)
    {
        Assert.Null(SettingValueValidator.Validate("time.date_format", value));
    }

    [Fact]
    public void Rejects_A_Separator_Only_Date_Format()
    {
        var error = SettingValueValidator.Validate("time.date_format", "---");

        Assert.NotNull(error);
        Assert.Equal("SETTINGS.INVALID_DATE_FORMAT", error.Value.Code);
    }
}
