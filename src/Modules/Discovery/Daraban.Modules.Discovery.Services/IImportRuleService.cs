using Daraban.Modules.Discovery.Data.Entities;

namespace Daraban.Modules.Discovery.Services;

/// <summary>
/// Service for managing import rules (GLPI-style).
/// Handles rule evaluation and device matching.
/// </summary>
public interface IImportRuleService
{
    // CRUD operations
    Task<ImportRuleResponse> CreateRuleAsync(CreateImportRuleRequest request, string? createdBy, CancellationToken ct = default);
    Task<ImportRuleResponse?> GetRuleByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ImportRuleResponse>> GetAllRulesAsync(CancellationToken ct = default);
    Task<List<ImportRuleResponse>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<ImportRuleResponse> UpdateRuleAsync(Guid id, UpdateImportRuleRequest request, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);

    // Rule evaluation
    Task<ImportRuleResult> EvaluateDeviceAsync(DeviceResponse device, CancellationToken ct = default);
    Task<List<ImportRuleResult>> EvaluateDevicesAsync(IEnumerable<DeviceResponse> devices, CancellationToken ct = default);
}

/// <summary>
/// Result of evaluating a device against import rules.
/// </summary>
public record ImportRuleResult(
    bool Matched,
    Guid? MatchedRuleId,
    string? MatchedRuleName,
    List<ImportRuleActionResponse> Actions,
    string? Reason
);

/// <summary>
/// DTOs for ImportRule.
/// </summary>
public record ImportRuleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    List<ImportRuleCriteriaResponse> Criteria,
    List<ImportRuleActionResponse> Actions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? CreatedBy
);

public record ImportRuleCriteriaResponse(
    Guid Id,
    string Field,
    string Operator,
    string Value
);

public record ImportRuleActionResponse(
    Guid Id,
    string ActionType,
    string? Value
);

public record CreateImportRuleRequest(
    string Name,
    string? Description,
    int Priority,
    List<CreateImportRuleCriteriaRequest> Criteria,
    List<CreateImportRuleActionRequest> Actions
);

public record UpdateImportRuleRequest(
    string? Name,
    string? Description,
    bool? IsActive,
    int? Priority,
    List<CreateImportRuleCriteriaRequest>? Criteria,
    List<CreateImportRuleActionRequest>? Actions
);

public record CreateImportRuleCriteriaRequest(
    string Field,
    string Operator,
    string Value
);

public record CreateImportRuleActionRequest(
    string ActionType,
    string? Value
);
