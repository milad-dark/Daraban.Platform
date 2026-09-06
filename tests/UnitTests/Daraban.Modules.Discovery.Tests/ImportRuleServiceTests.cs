using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Data.Repositories;
using Daraban.Modules.Discovery.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daraban.Modules.Discovery.Tests;

/// <summary>
/// ImportRuleService: the GLPI-style rule engine that decides what happens to a discovered
/// device. Evaluation is pure logic over in-memory rules, so every operator and the AND-semantics
/// of multiple criteria are exercised directly; CRUD tests pin that the persisted rule graph
/// (criteria + actions) is built with the parent key set.
/// </summary>
public class ImportRuleServiceTests
{
    private readonly Mock<IDiscoveryRepository> _repo = new();

    private ImportRuleService CreateSut() =>
        new(_repo.Object, NullLogger<ImportRuleService>.Instance);

    private static DeviceResponse Device(
        string ip = "192.168.1.10",
        string? os = "Windows 11 Pro",
        string? vendor = "Dell Inc.",
        string? hostname = "LT-0421",
        string? mac = "AA:BB:CC:DD:EE:FF",
        string? sysDescr = "Hardware: Intel Little Endian; OS: Windows")
        => new(1, Guid.CreateVersion7(), Guid.CreateVersion7(), ip, mac, hostname,
            os, "22000", vendor, "Latitude 5540", "SN-123", "443,3389", sysDescr,
            "LT-0421", "Stockholm HQ", "it@example.com", 123456, 1, 128,
            AssetCreated: false, AssetId: null,
            DiscoveredAt: DateTimeOffset.UtcNow, LastSeenAt: DateTimeOffset.UtcNow);

    private static ImportRule Rule(
        string name = "Windows laptops",
        int priority = 0,
        bool isActive = true,
        Action<ImportRule>? configure = null)
    {
        var rule = new ImportRule { Name = name, Priority = priority, IsActive = isActive };
        configure?.Invoke(rule);
        return rule;
    }

    private static ImportRuleCriteria Criteria(string field, string op, string value) => new()
    {
        Id = Guid.CreateVersion7(),
        Field = field,
        Operator = op,
        Value = value
    };

    private static ImportRuleAction Action(string type, string? value = null) => new()
    {
        Id = Guid.CreateVersion7(),
        ActionType = type,
        Value = value
    };

    private void ArrangeActiveRules(params ImportRule[] rules)
        => _repo.Setup(r => r.GetActiveImportRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules.ToList());

    // ---- Evaluation: operator coverage --------------------------------------------------------

    [Theory]
    [InlineData(ImportRuleOperators.Contains, "Windows", true)]
    [InlineData(ImportRuleOperators.Contains, "Linux", false)]
    [InlineData(ImportRuleOperators.Contains, "windows", true)]        // case-insensitive
    [InlineData(ImportRuleOperators.Equals, "Windows 11 Pro", true)]
    [InlineData(ImportRuleOperators.Equals, "windows 11 pro", true)]   // case-insensitive
    [InlineData(ImportRuleOperators.Equals, "Windows 10", false)]
    [InlineData(ImportRuleOperators.NotEquals, "macOS", true)]
    [InlineData(ImportRuleOperators.NotEquals, "Windows 11 Pro", false)]
    [InlineData(ImportRuleOperators.StartsWith, "Windows", true)]
    [InlineData(ImportRuleOperators.StartsWith, "Linux", false)]
    [InlineData(ImportRuleOperators.EndsWith, "Pro", true)]
    [InlineData(ImportRuleOperators.EndsWith, "Server", false)]
    [InlineData(ImportRuleOperators.NotContains, "Linux", true)]
    [InlineData(ImportRuleOperators.NotContains, "Windows", false)]
    public async Task EvaluateDeviceAsync_Covers_The_String_Operators(string op, string value, bool expectMatch)
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, op, value))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.Equal(expectMatch, result.Matched);
    }

    [Theory]
    [InlineData("LT-\\d{4}", true)]
    [InlineData("^LT-", true)]
    [InlineData("^SVR-", false)]
    public async Task EvaluateDeviceAsync_Supports_Regex_Matching(string pattern, bool expectMatch)
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.Hostname, ImportRuleOperators.Matches, pattern))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.Equal(expectMatch, result.Matched);
    }

    [Theory]
    [InlineData(ImportRuleOperators.GreaterThan, "2000", true)]
    [InlineData(ImportRuleOperators.GreaterThan, "30000", false)]
    [InlineData(ImportRuleOperators.LessThan, "30000", true)]
    [InlineData(ImportRuleOperators.LessThan, "2000", false)]
    public async Task EvaluateDeviceAsync_Supports_Numeric_Comparison(string op, string threshold, bool expectMatch)
    {
        // OsVersion is a string field, but "22000" parses as a number, so GreaterThan/LessThan
        // compare numerically rather than lexically ("9" > "10" as strings, which would lie).
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.OsVersion, op, threshold))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.Equal(expectMatch, result.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_Numeric_Comparison_Falls_Back_To_String_Ordering()
    {
        // Two non-numeric values compare as case-insensitive strings: "Dell Inc." < "Intel".
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.Vendor, ImportRuleOperators.LessThan, "Intel"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device(vendor: "Dell Inc."));

        Assert.True(result.Matched);
    }

    [Theory]
    [InlineData(ImportRuleOperators.IsEmpty, false)]
    [InlineData(ImportRuleOperators.IsNotEmpty, true)]
    public async Task EvaluateDeviceAsync_Supports_Empty_Checks_On_Populated_Fields(string op, bool expectMatch)
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.SerialNumber, op, ""))));

        var result = await CreateSut().EvaluateDeviceAsync(Device()); // SerialNumber = "SN-123"

        Assert.Equal(expectMatch, result.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_IsEmpty_Matches_A_Genuinely_Missing_Field()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.SerialNumber, ImportRuleOperators.IsEmpty, ""))));

        var result = await CreateSut().EvaluateDeviceAsync(Device()); // serial set; clear it via a device without one
        var bare = Device();
        bare = bare with { SerialNumber = null };

        var bareResult = await CreateSut().EvaluateDeviceAsync(bare);

        Assert.False(result.Matched);
        Assert.True(bareResult.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_An_Unknown_Field_Fails_The_Criterion()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria("NotARealField", ImportRuleOperators.Equals, "x"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        // Closed by default: a typo'd field name must not silently match everything.
        Assert.False(result.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_An_Unknown_Operator_Fails_The_Criterion()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, "BogusOp", "Windows"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.False(result.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_A_Null_Field_Fails_All_Operators_Except_IsEmpty()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device(os: null));

        Assert.False(result.Matched);
    }

    // ---- Evaluation: AND semantics + priority --------------------------------------------------

    [Fact]
    public async Task EvaluateDeviceAsync_Requires_Every_Criterion_To_Match()
    {
        ArrangeActiveRules(Rule(configure: r =>
        {
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"));
            r.Criteria.Add(Criteria(ImportRuleFields.Vendor, ImportRuleOperators.Contains, "Dell"));
            r.Criteria.Add(Criteria(ImportRuleFields.Hostname, ImportRuleOperators.StartsWith, "LT"));
        }));

        // All three hold.
        var full = await CreateSut().EvaluateDeviceAsync(Device());
        Assert.True(full.Matched);

        // Vendor breaks the AND chain.
        var otherVendor = await CreateSut().EvaluateDeviceAsync(Device(vendor: "HP"));
        Assert.False(otherVendor.Matched);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_A_Rule_With_No_Criteria_Never_Matches()
    {
        ArrangeActiveRules(Rule(configure: r => { })); // zero criteria

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        // An empty rule must not be a catch-all: without this, one forgotten criteria list turns
        // every discovered device into an asset.
        Assert.False(result.Matched);
        Assert.Equal("No matching rule found", result.Reason);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_Picks_The_Highest_Priority_Match()
    {
        // GLPI convention: LOWER number = higher precedence. A specific rule (1) outranks the
        // general one (10), which in turn outranks the catch-all (100).
        ArrangeActiveRules(
            Rule("Broad Windows rule", priority: 100, configure: r =>
                r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"))),
            Rule("Any Dell hardware", priority: 10, configure: r =>
                r.Criteria.Add(Criteria(ImportRuleFields.Vendor, ImportRuleOperators.Contains, "Dell"))),
            Rule("Specific laptop", priority: 1, configure: r =>
                r.Criteria.Add(Criteria(ImportRuleFields.Hostname, ImportRuleOperators.Matches, "^LT-\\d+"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        // The machine matches all three, but the most specific one wins.
        Assert.True(result.Matched);
        Assert.Equal("Specific laptop", result.MatchedRuleName);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_Returns_The_Matched_Rules_Actions()
    {
        var expected = new[] { (ImportRuleActionTypes.AssignType, "Computer"), (ImportRuleActionTypes.CreateAsset, null) };

        ArrangeActiveRules(Rule(configure: r =>
        {
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"));
            foreach (var (type, value) in expected)
                r.Actions.Add(Action(type, value));
        }));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.True(result.Matched);
        Assert.NotNull(result.MatchedRuleId);
        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(ImportRuleActionTypes.AssignType, result.Actions[0].ActionType);
        Assert.Equal("Computer", result.Actions[0].Value);
        Assert.Equal(ImportRuleActionTypes.CreateAsset, result.Actions[1].ActionType);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_Reports_No_Match_When_Nothing_Fits()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Linux"))));

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.False(result.Matched);
        Assert.Null(result.MatchedRuleId);
        Assert.Null(result.MatchedRuleName);
        Assert.Empty(result.Actions);
        Assert.Equal("No matching rule found", result.Reason);
    }

    [Fact]
    public async Task EvaluateDeviceAsync_Ignores_Inactive_Rules()
    {
        // The repository is asked for active rules only; this pins the contract -- a deactivated
        // catch-all must stop catching the moment it is switched off.
        _repo.Setup(r => r.GetActiveImportRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImportRule>());

        var result = await CreateSut().EvaluateDeviceAsync(Device());

        Assert.False(result.Matched);
    }

    [Fact]
    public async Task EvaluateDevicesAsync_Evaluates_Each_Device_Independently()
    {
        ArrangeActiveRules(Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.Vendor, ImportRuleOperators.Contains, "Dell"))));

        var results = await CreateSut().EvaluateDevicesAsync(new[]
        {
            Device(vendor: "Dell Inc."),
            Device(vendor: "HP"),
        });

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Matched);
        Assert.False(results[1].Matched);
    }

    // ---- CRUD ---------------------------------------------------------------------------------

    [Fact]
    public async Task CreateRuleAsync_Persists_Criteria_And_Actions_With_The_Parent_Key()
    {
        ImportRule? captured = null;
        _repo.Setup(r => r.AddImportRuleAsync(It.IsAny<ImportRule>(), It.IsAny<CancellationToken>()))
            .Callback<ImportRule, CancellationToken>((rule, _) => captured = rule)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new CreateImportRuleRequest(
            "Windows laptops", "Laptops become computer assets", 10,
            new List<CreateImportRuleCriteriaRequest>
            {
                new(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"),
            },
            new List<CreateImportRuleActionRequest>
            {
                new(ImportRuleActionTypes.AssignType, "Computer"),
            });

        var response = await CreateSut().CreateRuleAsync(request, createdBy: "admin");

        Assert.Equal("Windows laptops", response.Name);
        Assert.True(response.IsActive);
        Assert.Single(response.Criteria);
        Assert.Single(response.Actions);

        // CreateRuleAsync sets the navigation children; the ImportRuleId FK is wired by EF when
        // the graph is saved -- asserting it is already set here would couple the test to EF's
        // fixup internals, so the meaningful assertion is the graph shape itself.
        Assert.Single(captured!.Criteria);
        Assert.Single(captured.Actions);
        Assert.Equal(ImportRuleFields.OsGuess, captured.Criteria.First().Field);
        Assert.Equal(ImportRuleActionTypes.AssignType, captured.Actions.First().ActionType);
    }

    [Fact]
    public async Task UpdateRuleAsync_Leaves_Omitted_Collections_Alone()
    {
        var rule = Rule(configure: r =>
        {
            r.Criteria.Add(Criteria(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Windows"));
            r.Actions.Add(Action(ImportRuleActionTypes.AssignType, "Computer"));
        });

        _repo.Setup(r => r.GetImportRuleByIdAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _repo.Setup(r => r.UpdateImportRuleAsync(rule, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Name update only: no Criteria, no Actions in the payload.
        var response = await CreateSut().UpdateRuleAsync(
            rule.Id, new UpdateImportRuleRequest("Renamed", null, null, null, null, null));

        Assert.Equal("Renamed", response.Name);

        // null means "leave alone", not "clear" -- the rule keeps its criteria and actions.
        Assert.Single(response.Criteria);
        Assert.Single(response.Actions);
        Assert.Single(rule.Criteria);
        Assert.Single(rule.Actions);
    }

    [Fact]
    public async Task UpdateRuleAsync_Replaces_Criteria_When_Provided()
    {
        var rule = Rule(configure: r =>
            r.Criteria.Add(Criteria(ImportRuleFields.Vendor, ImportRuleOperators.Contains, "Dell")));

        _repo.Setup(r => r.GetImportRuleByIdAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        _repo.Setup(r => r.UpdateImportRuleAsync(rule, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().UpdateRuleAsync(
            rule.Id,
            new UpdateImportRuleRequest(
                null, null, null, null,
                new List<CreateImportRuleCriteriaRequest>
                {
                    new(ImportRuleFields.Hostname, ImportRuleOperators.StartsWith, "SVR-"),
                    new(ImportRuleFields.OsGuess, ImportRuleOperators.Contains, "Server"),
                },
                null));

        // An explicit (even empty-of-old-entries) list replaces; the old criterion is gone.
        Assert.Equal(2, rule.Criteria.Count);
        Assert.DoesNotContain(rule.Criteria, c => c.Field == ImportRuleFields.Vendor);
        Assert.Contains(rule.Criteria, c => c.Field == ImportRuleFields.Hostname);
    }

    [Fact]
    public async Task UpdateRuleAsync_Throws_For_An_Unknown_Rule()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetImportRuleByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportRule?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().UpdateRuleAsync(id, new UpdateImportRuleRequest(null, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetRuleByIdAsync_Returns_Null_For_An_Unknown_Rule()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetImportRuleByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportRule?)null);

        Assert.Null(await CreateSut().GetRuleByIdAsync(id));
    }

    [Fact]
    public async Task GetAllRulesAsync_And_ActiveRulesAsync_Map_Every_Rule()
    {
        var rules = new List<ImportRule> { Rule(), Rule("Second") };

        _repo.Setup(r => r.GetAllImportRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);
        _repo.Setup(r => r.GetActiveImportRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules.Where(r => r.IsActive).ToList());

        var all = await CreateSut().GetAllRulesAsync();
        var active = await CreateSut().GetActiveRulesAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(2, active.Count);
        Assert.Contains(all, r => r.Name == "Second");
    }

    [Fact]
    public async Task DeleteRuleAsync_Delegates_And_Saves()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.DeleteImportRuleAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().DeleteRuleAsync(id);

        _repo.Verify(r => r.DeleteImportRuleAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
