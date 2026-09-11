using Daraban.Modules.Settings.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Settings.Services;

/// <summary>Application service for platform settings (Task 7.4).</summary>
public interface ISettingsService
{
    /// <summary>All settings grouped by category, in UI tab order (GET /api/v1/settings).</summary>
    Task<Result<SettingsByCategoryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Updates one setting's value (PUT /api/v1/settings/{key}).</summary>
    Task<Result<SettingDto>> UpdateAsync(string key, UpdateSettingRequest request, Guid actorId, CancellationToken ct = default);

    /// <summary>Connectivity test for the category that owns a key
    /// (POST /api/v1/settings/test/{key}).</summary>
    Task<Result<ConnectionTestResultDto>> TestConnectionAsync(string key, CancellationToken ct = default);
}
