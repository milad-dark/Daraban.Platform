using System.Data.Common;
using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Services.Dtos;
using Daraban.Platform.Common;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Settings feature implementation (Task 7.4). Reads come from <see cref="SystemSettingCache"/>,
/// writes validate against <see cref="SettingCatalog"/> and go through the cache (which owns
/// the DB write and both cache layers). Secrets are masked at the DTO boundary -- the full
/// value never leaves the service layer. All expected failures return <see cref="Result{T}"/>;
/// no exceptions for control flow.
/// </summary>
public sealed class SettingsService(
    SystemSettingCache cache,
    IConnectivityTester tester,
    ILogger<SettingsService> logger) : ISettingsService
{
    /// <summary>Placeholder shown for every secret. Constant so the UI can detect it.</summary>
    public const string SecretMask = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";

    public async Task<Result<SettingsByCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        await cache.EnsureLoadedAsync(ct);

        var categories = SettingCategories.All
            .Select(category => new CategorySettingsDto(
                category,
                cache.All.Where(s => s.Category == category).Select(ToDto).ToList()))
            .ToList();

        return Result.Success(new SettingsByCategoryDto(categories));
    }

    public async Task<Result<SettingDto>> UpdateAsync(
        string key, UpdateSettingRequest request, Guid actorId, CancellationToken ct = default)
    {
        await cache.EnsureLoadedAsync(ct);

        var current = cache.Get(key);
        if (current is null)
        {
            return Result.Failure<SettingDto>(
                new Error("SETTINGS.KEY_UNKNOWN", $"Unknown setting '{key}'.", ErrorType.NotFound));
        }

        // The UI round-trips the mask when the admin did not touch a password field.
        // Treat that as "keep current value" rather than writing 8 bullets into the DB --
        // checked BEFORE validation, because the mask itself is not a valid value.
        if (current.IsSecret && request.Value == SecretMask)
        {
            return Result.Success(ToDto(current));
        }

        var failure = SettingValueValidator.Validate(key, request.Value);
        if (failure is not null)
        {
            return Result.Failure<SettingDto>(
                new Error(failure.Value.Code, failure.Value.Message, ErrorType.Validation));
        }

        try
        {
            await cache.UpdateAsync(key, request.Value, actorId, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbException)
        {
            // Intentionally narrow: persistence-layer failures only. Unexpected bugs must
            // crash loudly rather than be swallowed as a business error.
            logger.LogError(ex, "Failed to update setting {Key}", key);
            return Result.Failure<SettingDto>(
                new Error("SETTINGS.UPDATE_FAILED", "The setting could not be saved.", ErrorType.BusinessRule));
        }

        logger.LogInformation("Setting {Key} updated by {ActorId}", key, actorId);

        return Result.Success(ToDto(cache.Get(key) ?? current));
    }

    public async Task<Result<ConnectionTestResultDto>> TestConnectionAsync(string key, CancellationToken ct = default)
    {
        // Route by the category the key belongs to -- "test the email settings" and
        // "test the LDAP settings" are the two tests the UI offers today.
        var definition = SettingCatalog.Find(key);
        if (definition is null)
        {
            return Result.Failure<ConnectionTestResultDto>(
                new Error("SETTINGS.KEY_UNKNOWN", $"Unknown setting '{key}'.", ErrorType.NotFound));
        }

        await cache.EnsureLoadedAsync(ct);

        return definition.Category switch
        {
            SettingCategories.Email => await TestEmailAsync(ct),
            SettingCategories.Ldap => await TestLdapAsync(ct),
            _ => Result.Failure<ConnectionTestResultDto>(
                new Error("SETTINGS.TEST_NOT_SUPPORTED",
                    $"No connectivity test exists for the '{definition.Category}' category.",
                    ErrorType.Validation)),
        };
    }

    private async Task<Result<ConnectionTestResultDto>> TestEmailAsync(CancellationToken ct)
    {
        var smtp = new SmtpSettings(
            Host: Value("email.smtp_host"),
            Port: ParsePort("email.smtp_port"),
            User: Value("email.smtp_user"),
            Password: Value("email.smtp_password"),
            TlsMode: Value("email.smtp_tls"),
            FromAddress: Value("email.from_address"));

        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            return Result.Failure<ConnectionTestResultDto>(
                new Error("SETTINGS.TEST_NOT_CONFIGURED", "SMTP host is not configured yet.", ErrorType.Validation));
        }

        return Result.Success(await tester.TestEmailAsync(smtp, ct));
    }

    private async Task<Result<ConnectionTestResultDto>> TestLdapAsync(CancellationToken ct)
    {
        var ldap = new LdapConnectionSettings(
            Server: Value("ldap.server"),
            Port: ParsePort("ldap.port"),
            BaseDn: Value("ldap.base_dn"),
            BindUser: Value("ldap.bind_user"),
            BindPassword: Value("ldap.bind_password"));

        if (string.IsNullOrWhiteSpace(ldap.Server))
        {
            return Result.Failure<ConnectionTestResultDto>(
                new Error("SETTINGS.TEST_NOT_CONFIGURED", "LDAP server is not configured yet.", ErrorType.Validation));
        }

        return Result.Success(await tester.TestLdapAsync(ldap, ct));
    }

    private string Value(string key) => cache.Get(key)?.Value ?? string.Empty;

    private int ParsePort(string key)
    {
        // Catalog validation guarantees Int settings parse, but the cached value could have
        // been written by an older code path -- default to a harmless port rather than throw.
        return int.TryParse(Value(key), out var port) ? port : 0;
    }

    private static SettingDto ToDto(SystemSetting s) => new(
        s.Key,
        s.IsSecret ? SecretMask : s.Value,
        s.ValueType,
        s.Category,
        s.Description,
        s.IsSecret,
        s.UpdatedAt);
}
