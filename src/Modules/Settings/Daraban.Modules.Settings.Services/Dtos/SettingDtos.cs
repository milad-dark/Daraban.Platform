using Daraban.Platform.Common;

namespace Daraban.Modules.Settings.Services.Dtos;

/// <summary>One setting as exposed by GET /api/v1/settings. Secret values are always
/// masked server-side -- the API never emits a credential, only its presence and
/// modification timestamp.</summary>
public sealed record SettingDto(
    string Key,
    string Value,
    string ValueType,
    string Category,
    string Description,
    bool IsSecret,
    DateTimeOffset UpdatedAt);

/// <summary>Settings grouped by category, in the tab order the UI renders.</summary>
public sealed record SettingsByCategoryDto(IReadOnlyList<CategorySettingsDto> Categories);

/// <summary>All settings of one category.</summary>
public sealed record CategorySettingsDto(string Category, IReadOnlyList<SettingDto> Settings);

/// <summary>Request body for PUT /api/v1/settings/{key}.</summary>
public sealed record UpdateSettingRequest(string Value);

/// <summary>Result of a connectivity test (POST /api/v1/settings/test/{key}).</summary>
public sealed record ConnectionTestResultDto(bool Success, string Message, int LatencyMs);

/// <summary>Typed view of the SMTP settings, as consumed by the connectivity tester.</summary>
public sealed record SmtpSettings(
    string Host, int Port, string User, string Password, string TlsMode, string FromAddress);

/// <summary>Typed view of the LDAP settings needed to reach the directory.</summary>
public sealed record LdapConnectionSettings(string Server, int Port, string BaseDn, string BindUser, string BindPassword);
