using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// AuditLogService (Task 7.3) -- the read side of the audit trail. The repository is mocked
/// because what is under test here is validation, pagination caps and DTO mapping, not
/// LINQ-to-PostgreSQL (that path is exercised by the interceptor tests, which run against
/// a real provider). Page-size caps are a resource-exhaustion guard on an append-only
/// table that grows without bound, so they are pinned by tests deliberately.
/// </summary>
public class AuditLogServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EntityRowId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IAuditLogRepository> _repo = new();
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        // Default: no actor rows exist, so name resolution returns an empty map.
        _repo
            .Setup(r => r.GetActorDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _sut = new AuditLogService(_repo.Object);
    }

    private void SetupPage(params AuditLog[] entries)
    {
        _repo
            .Setup(r => r.GetPagedAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.ToList() as IReadOnlyList<AuditLog>, entries.Length));
    }

    // ---- Input validation -----------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Rejects_Unknown_Action_Filter()
    {
        var result = await _sut.SearchAsync(null, null, null, "Exploded", null, null, 1, 20);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUDIT.INVALID_ACTION", result.Error!.Code);
    }

    [Theory]
    [InlineData("Added")]
    [InlineData("modified")] // case-insensitive on purpose
    [InlineData("Deleted")]
    public async Task SearchAsync_Accepts_The_Three_EF_State_Names(string action)
    {
        SetupPage();

        var result = await _sut.SearchAsync(null, null, null, action, null, null, 1, 20);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SearchAsync_Rejects_An_Inverted_Date_Range()
    {
        var result = await _sut.SearchAsync(
            null, null, null, null,
            from: new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
            1, 20);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUDIT.INVALID_RANGE", result.Error!.Code);
    }

    [Fact]
    public async Task SearchAsync_Treats_A_Nonpositive_Page_As_One()
    {
        SetupPage();

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 0, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Page);
    }

    [Fact]
    public async Task SearchAsync_Nonpositive_PageSize_Falls_Back_To_Default()
    {
        SetupPage();

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 1, 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.PageSize);
    }

    [Fact]
    public async Task SearchAsync_Caps_PageSize_At_200()
    {
        SetupPage();

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 1, 100_000);

        // A client asking for the whole table must be stopped at the cap, or one request
        // could serialize an unbounded slice of an append-only table.
        Assert.Equal(200, result.Value!.PageSize);
    }

    [Fact]
    public async Task SearchAsync_Passes_Page_Based_Paging_As_Skip_And_Take()
    {
        SetupPage();

        await _sut.SearchAsync(null, null, null, null, null, null, 3, 50);

        _repo.Verify(r => r.GetPagedAsync(
            null, null, null, null, null, null,
            100, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_Caps_The_History_Limit()
    {
        _repo
            .Setup(r => r.GetEntityHistoryAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetEntityHistoryAsync("User", EntityRowId, limit: 5_000);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.GetEntityHistoryAsync("User", EntityRowId, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEntityHistoryAsync_Rejects_A_Blank_Entity_Type()
    {
        var result = await _sut.GetEntityHistoryAsync("  ", EntityRowId, limit: 50);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUDIT.INVALID_ENTITY_TYPE", result.Error!.Code);
    }

    // ---- DTO mapping ------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Uses_The_Batched_Actor_Name_Lookup_Once_Per_Page()
    {
        SetupPage(
            MakeEntry(1, actorId: ActorId),
            MakeEntry(2, actorId: ActorId),
            MakeEntry(3, actorId: null));

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 1, 20);

        // One query per page, never one per row -- that is the whole point of the batch lookup.
        _repo.Verify(r => r.GetActorDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Value!.Items.Count(e => e.ActorUserId == ActorId));
        Assert.Equal(1, result.Value!.Items.Count(e => e.ActorUserId is null));
    }

    [Fact]
    public async Task SearchAsync_Resolves_Actor_Display_Names()
    {
        SetupPage(MakeEntry(1, actorId: ActorId));
        _repo
            .Setup(r => r.GetActorDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [ActorId] = "Milad E." });

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 1, 20);

        Assert.Equal("Milad E.", result.Value!.Items[0].ActorName);
    }

    [Fact]
    public async Task SearchAsync_Leaves_ActorName_Empty_For_Unknown_Or_Deleted_Actors()
    {
        SetupPage(MakeEntry(1, actorId: ActorId));

        var result = await _sut.SearchAsync(null, null, null, null, null, null, 1, 20);

        // Deleted users must not break the page -- the id stays for forensics, the name is just absent.
        Assert.Null(result.Value!.Items[0].ActorName);
        Assert.Equal(ActorId, result.Value!.Items[0].ActorUserId);
    }

    [Fact]
    public async Task GetEntityHistoryAsync_Maps_Old_And_New_Values_For_The_Diff_Viewer()
    {
        _repo
            .Setup(r => r.GetEntityHistoryAsync("User", EntityRowId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEntry(1, oldValues: """{"Status":"Active"}""", newValues: """{"Status":"Suspended"}""")]);

        var result = await _sut.GetEntityHistoryAsync("User", EntityRowId, limit: 50);

        var dto = Assert.Single(result.Value!);
        Assert.Equal("""{"Status":"Active"}""", dto.OldValues);
        Assert.Equal("""{"Status":"Suspended"}""", dto.NewValues);
    }

    private static AuditLog MakeEntry(long id, Guid? actorId = null, string? oldValues = null, string? newValues = null)
        => new()
        {
            Id = id,
            EntityType = "User",
            EntityId = EntityRowId,
            Action = "Modified",
            ActorUserId = actorId,
            OldValues = oldValues,
            NewValues = newValues,
            OccurredAt = DateTimeOffset.UtcNow,
        };
}
