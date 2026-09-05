using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Assets;
using Moq;
using Xunit;

namespace Daraban.Modules.Assets.Tests;

/// <summary>
/// AssetLifecycleService: the transition state machine, the reason requirement on Retire and
/// Dispose, the forced unassign, and the published event. The state machine is the part most
/// worth pinning — it is the only thing preventing a disposed asset from re-entering service.
/// </summary>
public class AssetLifecycleServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IAssetRepository> _assets = new(MockBehavior.Strict);
    private readonly Mock<IAssetStatusHistoryRepository> _history = new(MockBehavior.Strict);
    private readonly Mock<IAssetAssignmentRepository> _assignments = new(MockBehavior.Strict);
    private readonly Mock<IEventPublisher> _events = new();

    private AssetLifecycleService CreateSut() =>
        new(_assets.Object, _history.Object, _assignments.Object, _events.Object);

    private static Asset AssetWith(AssetStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        AssetTypeId = Guid.CreateVersion7(),
        EntityNodeId = Guid.CreateVersion7(),
        Name = "Dell XPS",
        Status = status,
    };

    /// <summary>Sets up the happy path: asset found, no current assignment, writes succeed.</summary>
    private void ArrangeTransition(Asset asset, AssetAssignment? currentAssignment = null)
    {
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssignment);
        if (currentAssignment is not null)
            _assignments.Setup(r => r.UpdateAsync(currentAssignment, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        _history.Setup(r => r.AddAsync(It.IsAny<AssetStatusHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ---- Allowed transitions -----------------------------------------------------------------

    [Theory]
    [InlineData(AssetStatus.InStock, AssetStatus.Archived)]
    [InlineData(AssetStatus.InUse, AssetStatus.UnderMaintenance)]
    [InlineData(AssetStatus.InUse, AssetStatus.Archived)]
    [InlineData(AssetStatus.UnderMaintenance, AssetStatus.InUse)]
    [InlineData(AssetStatus.UnderMaintenance, AssetStatus.Archived)]
    [InlineData(AssetStatus.Archived, AssetStatus.InStock)]
    public async Task TransitionAsync_Allows_The_Documented_Paths(AssetStatus from, AssetStatus to)
    {
        var asset = AssetWith(from);
        ArrangeTransition(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, null, null), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, asset.Status);
        Assert.Equal(from, result.Value.FromStatus);
        Assert.Equal(to, result.Value.ToStatus);
    }

    [Theory]
    [InlineData(AssetStatus.InStock, AssetStatus.Retired)]
    [InlineData(AssetStatus.InUse, AssetStatus.Retired)]
    [InlineData(AssetStatus.UnderMaintenance, AssetStatus.Retired)]
    [InlineData(AssetStatus.Retired, AssetStatus.Disposed)]
    public async Task TransitionAsync_Allows_Retire_And_Dispose_When_A_Reason_Is_Given(
        AssetStatus from, AssetStatus to)
    {
        var asset = AssetWith(from);
        ArrangeTransition(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, "End of life", null), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, asset.Status);
        Assert.Equal("End of life", result.Value.Reason);
    }

    // ---- Rejected transitions ----------------------------------------------------------------

    [Theory]
    [InlineData(AssetStatus.Disposed, AssetStatus.InStock)]
    [InlineData(AssetStatus.Disposed, AssetStatus.InUse)]
    [InlineData(AssetStatus.Disposed, AssetStatus.Retired)]
    [InlineData(AssetStatus.Disposed, AssetStatus.Archived)]
    public async Task TransitionAsync_Treats_Disposed_As_Fully_Terminal(AssetStatus from, AssetStatus to)
    {
        var asset = AssetWith(from);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, "changed my mind", null), ActorId);

        // A disposed asset physically no longer exists. Nothing brings it back.
        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.INVALID_TRANSITION", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
    }

    [Theory]
    [InlineData(AssetStatus.Archived, AssetStatus.InUse)]      // must go via InStock
    [InlineData(AssetStatus.Archived, AssetStatus.Retired)]
    [InlineData(AssetStatus.InStock, AssetStatus.InUse)]       // assignment does this, not lifecycle
    [InlineData(AssetStatus.InStock, AssetStatus.UnderMaintenance)]
    [InlineData(AssetStatus.Retired, AssetStatus.InStock)]
    [InlineData(AssetStatus.InUse, AssetStatus.InStock)]
    public async Task TransitionAsync_Rejects_Undocumented_Paths(AssetStatus from, AssetStatus to)
    {
        var asset = AssetWith(from);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, "reason", null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.INVALID_TRANSITION", result.Error!.Code);

        // Nothing recorded, nothing published, status untouched.
        Assert.Equal(from, asset.Status);
        _history.Verify(r => r.AddAsync(
            It.IsAny<AssetStatusHistory>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.PublishAsync(
            It.IsAny<AssetLifecycleChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransitionAsync_Rejects_A_Self_Transition()
    {
        var asset = AssetWith(AssetStatus.InUse);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(AssetStatus.InUse, null, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.INVALID_TRANSITION", result.Error!.Code);
    }

    // ---- Reason requirement ------------------------------------------------------------------

    [Theory]
    [InlineData(AssetStatus.Retired, null)]
    [InlineData(AssetStatus.Retired, "")]
    [InlineData(AssetStatus.Retired, "   ")]
    public async Task TransitionAsync_Requires_A_Reason_To_Retire(AssetStatus to, string? reason)
    {
        var asset = AssetWith(AssetStatus.InUse);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, reason, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.REASON_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task TransitionAsync_Requires_A_Reason_To_Dispose()
    {
        var asset = AssetWith(AssetStatus.Retired);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(AssetStatus.Disposed, null, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.REASON_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task TransitionAsync_Does_Not_Require_A_Reason_To_Archive()
    {
        var asset = AssetWith(AssetStatus.InUse);
        ArrangeTransition(asset);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(AssetStatus.Archived, null, null), ActorId);

        // Archiving is reversible (Archived -> InStock), so it does not demand justification.
        Assert.True(result.IsSuccess);
    }

    // ---- Side effects ------------------------------------------------------------------------

    [Theory]
    [InlineData(AssetStatus.Retired)]
    [InlineData(AssetStatus.Disposed)]
    public async Task TransitionAsync_Unassigns_The_Holder_When_Leaving_Service(AssetStatus to)
    {
        var from = to == AssetStatus.Disposed ? AssetStatus.Retired : AssetStatus.InUse;
        var asset = AssetWith(from);
        var assignment = new AssetAssignment
        {
            Id = Guid.CreateVersion7(),
            AssetId = asset.Id,
            TargetId = Guid.CreateVersion7(),
            IsCurrent = true,
        };

        ArrangeTransition(asset, assignment);

        var result = await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(to, "End of life", null), ActorId);

        Assert.True(result.IsSuccess);

        // Leaving an asset assigned to a person after it has been scrapped would keep it on that
        // person's inventory forever.
        Assert.False(assignment.IsCurrent);
        Assert.NotNull(assignment.UnassignedAt);
    }

    [Fact]
    public async Task TransitionAsync_Does_Not_Touch_Assignments_When_Archiving()
    {
        var asset = AssetWith(AssetStatus.InUse);
        ArrangeTransition(asset);

        await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(AssetStatus.Archived, null, null), ActorId);

        // Only Retire/Dispose forcibly unassign. Archiving preserves the holder so a restore
        // does not lose that context.
        _assignments.Verify(r => r.UpdateAsync(
            It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransitionAsync_Records_History_With_Actor_And_Both_Statuses()
    {
        var asset = AssetWith(AssetStatus.InUse);
        AssetStatusHistory? captured = null;

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);
        _history.Setup(r => r.AddAsync(It.IsAny<AssetStatusHistory>(), It.IsAny<CancellationToken>()))
            .Callback<AssetStatusHistory, CancellationToken>((h, _) => captured = h)
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().TransitionAsync(
            asset.Id,
            new LifecycleTransitionRequest(AssetStatus.UnderMaintenance, null, "Screen flicker"),
            ActorId);

        Assert.Equal(AssetStatus.InUse, captured!.FromStatus);
        Assert.Equal(AssetStatus.UnderMaintenance, captured.ToStatus);
        Assert.Equal(ActorId, captured.ActorUserId);
        Assert.Equal("Screen flicker", captured.Notes);
    }

    [Fact]
    public async Task TransitionAsync_Publishes_The_Lifecycle_Event_With_Both_Statuses_As_Strings()
    {
        var asset = AssetWith(AssetStatus.InUse);
        ArrangeTransition(asset);

        await CreateSut().TransitionAsync(
            asset.Id, new LifecycleTransitionRequest(AssetStatus.Retired, "Lease ended", null), ActorId);

        _events.Verify(e => e.PublishAsync(
            It.Is<AssetLifecycleChangedEvent>(evt =>
                evt.AssetId == asset.Id &&
                evt.EntityId == asset.EntityNodeId &&
                evt.FromStatus == "InUse" &&
                evt.ToStatus == "Retired" &&
                evt.ActorUserId == ActorId &&
                evt.Reason == "Lease ended"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransitionAsync_Returns_NotFound_For_A_Missing_Asset()
    {
        var id = Guid.CreateVersion7();
        _assets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().TransitionAsync(
            id, new LifecycleTransitionRequest(AssetStatus.Archived, null, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_FOUND", result.Error!.Code);
    }

    // ---- History read ------------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_Returns_NotFound_For_A_Missing_Asset()
    {
        var id = Guid.CreateVersion7();
        _assets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().GetHistoryAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_Maps_Every_Transition()
    {
        var asset = AssetWith(AssetStatus.Retired);
        var history = new[]
        {
            new AssetStatusHistory
            {
                Id = Guid.CreateVersion7(), AssetId = asset.Id,
                FromStatus = AssetStatus.InStock, ToStatus = AssetStatus.InUse,
                ActorUserId = ActorId, OccurredAt = DateTimeOffset.UtcNow.AddDays(-10),
            },
            new AssetStatusHistory
            {
                Id = Guid.CreateVersion7(), AssetId = asset.Id,
                FromStatus = AssetStatus.InUse, ToStatus = AssetStatus.Retired,
                ActorUserId = ActorId, Reason = "End of life", OccurredAt = DateTimeOffset.UtcNow,
            },
        };

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _history.Setup(r => r.GetByAssetIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(history);

        var result = await CreateSut().GetHistoryAsync(asset.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("End of life", result.Value[1].Reason);
    }
}
