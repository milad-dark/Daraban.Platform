using System.Text.RegularExpressions;
using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Discovery.Services;

/// <summary>
/// Service for managing import rules (GLPI-style).
/// Handles rule evaluation and device matching.
/// </summary>
public class ImportRuleService : IImportRuleService
{
    private readonly IDiscoveryRepository _repository;
    private readonly ILogger<ImportRuleService> _logger;

    public ImportRuleService(IDiscoveryRepository repository, ILogger<ImportRuleService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create a new import rule.
    /// </summary>
    public async Task<ImportRuleResponse> CreateRuleAsync(CreateImportRuleRequest request, string? createdBy, CancellationToken ct = default)
    {
        var rule = new ImportRule
        {
            Name = request.Name,
            Description = request.Description,
            Priority = request.Priority,
            CreatedBy = createdBy
        };

        foreach (var criteriaRequest in request.Criteria)
        {
            rule.Criteria.Add(new ImportRuleCriteria
            {
                Field = criteriaRequest.Field,
                Operator = criteriaRequest.Operator,
                Value = criteriaRequest.Value
            });
        }

        foreach (var actionRequest in request.Actions)
        {
            rule.Actions.Add(new ImportRuleAction
            {
                ActionType = actionRequest.ActionType,
                Value = actionRequest.Value
            });
        }

        await _repository.AddImportRuleAsync(rule, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Created import rule {RuleId}: {RuleName}", rule.Id, rule.Name);

        return MapToResponse(rule);
    }

    /// <summary>
    /// Get an import rule by ID.
    /// </summary>
    public async Task<ImportRuleResponse?> GetRuleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _repository.GetImportRuleByIdAsync(id, ct);
        return rule != null ? MapToResponse(rule) : null;
    }

    /// <summary>
    /// Get all import rules.
    /// </summary>
    public async Task<List<ImportRuleResponse>> GetAllRulesAsync(CancellationToken ct = default)
    {
        var rules = await _repository.GetAllImportRulesAsync(ct);
        return rules.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Get all active import rules.
    /// </summary>
    public async Task<List<ImportRuleResponse>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        var rules = await _repository.GetActiveImportRulesAsync(ct);
        return rules.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Update an import rule.
    /// </summary>
    public async Task<ImportRuleResponse> UpdateRuleAsync(Guid id, UpdateImportRuleRequest request, CancellationToken ct = default)
    {
        var rule = await _repository.GetImportRuleByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Import rule with ID '{id}' not found.");

        if (request.Name != null)
            rule.Name = request.Name;
        if (request.Description != null)
            rule.Description = request.Description;
        if (request.IsActive.HasValue)
            rule.IsActive = request.IsActive.Value;
        if (request.Priority.HasValue)
            rule.Priority = request.Priority.Value;

        // Update criteria if provided
        if (request.Criteria != null)
        {
            rule.Criteria.Clear();
            foreach (var criteriaRequest in request.Criteria)
            {
                rule.Criteria.Add(new ImportRuleCriteria
                {
                    ImportRuleId = id,
                    Field = criteriaRequest.Field,
                    Operator = criteriaRequest.Operator,
                    Value = criteriaRequest.Value
                });
            }
        }

        // Update actions if provided
        if (request.Actions != null)
        {
            rule.Actions.Clear();
            foreach (var actionRequest in request.Actions)
            {
                rule.Actions.Add(new ImportRuleAction
                {
                    ImportRuleId = id,
                    ActionType = actionRequest.ActionType,
                    Value = actionRequest.Value
                });
            }
        }

        rule.ModifiedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateImportRuleAsync(rule, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Updated import rule {RuleId}: {RuleName}", rule.Id, rule.Name);

        return MapToResponse(rule);
    }

    /// <summary>
    /// Delete an import rule.
    /// </summary>
    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteImportRuleAsync(id, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted import rule {RuleId}", id);
    }

    /// <summary>
    /// Evaluate a device against all active import rules.
    /// Returns the first matching rule and its actions.
    /// </summary>
    public async Task<ImportRuleResult> EvaluateDeviceAsync(DeviceResponse device, CancellationToken ct = default)
    {
        var rules = await _repository.GetActiveImportRulesAsync(ct);

        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            if (EvaluateCriteria(device, rule.Criteria))
            {
                _logger.LogInformation(
                    "Device {IpAddress} matched rule {RuleName} (Priority: {Priority})",
                    device.IpAddress, rule.Name, rule.Priority);

                return new ImportRuleResult(
                    Matched: true,
                    MatchedRuleId: rule.Id,
                    MatchedRuleName: rule.Name,
                    Actions: rule.Actions.Select(a => new ImportRuleActionResponse(a.Id, a.ActionType, a.Value)).ToList(),
                    Reason: $"Matched by rule: {rule.Name}"
                );
            }
        }

        return new ImportRuleResult(
            Matched: false,
            MatchedRuleId: null,
            MatchedRuleName: null,
            Actions: new List<ImportRuleActionResponse>(),
            Reason: "No matching rule found"
        );
    }

    /// <summary>
    /// Evaluate multiple devices against import rules.
    /// </summary>
    public async Task<List<ImportRuleResult>> EvaluateDevicesAsync(IEnumerable<DeviceResponse> devices, CancellationToken ct = default)
    {
        var results = new List<ImportRuleResult>();

        foreach (var device in devices)
        {
            var result = await EvaluateDeviceAsync(device, ct);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Evaluate criteria against a device.
    /// All criteria must match (AND logic).
    /// </summary>
    private bool EvaluateCriteria(DeviceResponse device, ICollection<ImportRuleCriteria> criteria)
    {
        if (criteria == null || criteria.Count == 0)
            return false; // No criteria = no match

        foreach (var criterion in criteria)
        {
            var fieldValue = GetFieldValue(device, criterion.Field);
            if (!EvaluateSingleCriteria(fieldValue, criterion.Operator, criterion.Value))
            {
                return false; // One criterion failed = no match
            }
        }

        return true; // All criteria passed
    }

    /// <summary>
    /// Get the value of a field from a device.
    /// </summary>
    private string? GetFieldValue(DeviceResponse device, string field)
    {
        return field switch
        {
            ImportRuleFields.IpAddress => device.IpAddress,
            ImportRuleFields.MacAddress => device.MacAddress,
            ImportRuleFields.Hostname => device.Hostname,
            ImportRuleFields.OsGuess => device.OsGuess,
            ImportRuleFields.OsVersion => device.OsVersion,
            ImportRuleFields.Vendor => device.Vendor,
            ImportRuleFields.Model => device.Model,
            ImportRuleFields.SerialNumber => device.SerialNumber,
            ImportRuleFields.OpenPorts => device.OpenPorts,
            ImportRuleFields.SysDescr => device.SysDescr,
            ImportRuleFields.SysName => device.SysName,
            _ => null
        };
    }

    /// <summary>
    /// Evaluate a single criterion against a field value.
    /// </summary>
    private bool EvaluateSingleCriteria(string? fieldValue, string operatorType, string criterionValue)
    {
        // Handle null/empty cases
        if (operatorType == ImportRuleOperators.IsEmpty)
            return string.IsNullOrEmpty(fieldValue);

        if (operatorType == ImportRuleOperators.IsNotEmpty)
            return !string.IsNullOrEmpty(fieldValue);

        // If field is null/empty and operator is not IsEmpty, fail
        if (string.IsNullOrEmpty(fieldValue))
            return false;

        // Case-insensitive comparison for string operators
        var comparisonValue = criterionValue ?? string.Empty;

        return operatorType switch
        {
            ImportRuleOperators.Contains => fieldValue.Contains(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.NotContains => !fieldValue.Contains(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.Equals => fieldValue.Equals(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.NotEquals => !fieldValue.Equals(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.StartsWith => fieldValue.StartsWith(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.EndsWith => fieldValue.EndsWith(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ImportRuleOperators.Matches => Regex.IsMatch(fieldValue, comparisonValue),
            ImportRuleOperators.GreaterThan => CompareValues(fieldValue, comparisonValue) > 0,
            ImportRuleOperators.LessThan => CompareValues(fieldValue, comparisonValue) < 0,
            _ => false
        };
    }

    /// <summary>
    /// Compare two values (numeric or string).
    /// </summary>
    private int CompareValues(string value1, string value2)
    {
        // Try numeric comparison first
        if (long.TryParse(value1, out var num1) && long.TryParse(value2, out var num2))
        {
            return num1.CompareTo(num2);
        }

        // Fall back to string comparison
        return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Map entity to response DTO.
    /// </summary>
    private ImportRuleResponse MapToResponse(ImportRule rule) =>
        new(
            Id: rule.Id,
            Name: rule.Name,
            Description: rule.Description,
            IsActive: rule.IsActive,
            Priority: rule.Priority,
            Criteria: rule.Criteria.Select(c => new ImportRuleCriteriaResponse(c.Id, c.Field, c.Operator, c.Value)).ToList(),
            Actions: rule.Actions.Select(a => new ImportRuleActionResponse(a.Id, a.ActionType, a.Value)).ToList(),
            CreatedAt: rule.CreatedAt,
            ModifiedAt: rule.ModifiedAt,
            CreatedBy: rule.CreatedBy
        );
}
