using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Assets.Tests;

/// <summary>
/// AssetAssignmentService: which statuses block assignment, the implicit unassign of the
/// incumbent holder, and the InStock -> InUse side effect.
/// </summary>
public class AssetAssignmentServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TargetId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IAssetAssignmentRepository> _assignments = new(MockBehavior.Strict);
    private readonly Mock<IAssetRepository> _assets = new(MockBehavior.Strict);

    private AssetAssignmentService CreateSut() => new(_assignments.Object, _assets.Object);

    private static Asset AssetWith(AssetStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        AssetTypeId = Guid.CreateVersion7(),
        EntityNodeId = Guid.CreateVersion7(),
        Name = "Dell XPS",
        Status = status,
    };

    private static AssignAssetRequest Request(
        AssignmentTargetType type = AssignmentTargetType.User, string? notes = null)
        => new(type, TargetId, "Alice", notes);

    private static AssetAssignment CurrentAssignment(Guid assetId) => new()
    {
        Id = Guid.CreateVersion7(),
        AssetId = assetId,
        TargetType = AssignmentTargetType.User,
        TargetId = Guid.CreateVersion7(),
        TargetName = "Bob",
        AssignedAt = DateTimeOffset.UtcNow.AddDays(-30),
        IsCurrent = true,
    };

    // ---- Assign ------------------------------------------------------------------------------

    [Fact]
    public async Task AssignAsync_Returns_NotFound_For_A_Missing_Asset()
    {
        var id = Guid.CreateVersion7();
        _assets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().AssignAsync(id, Request(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_FOUND", result.Error!.Code);
    }

    [Theory]
    [InlineData(AssetStatus.Archived)]
    [InlineData(AssetStatus.Retired)]
    [InlineData(AssetStatus.Disposed)]
    public async Task AssignAsync_Refuses_Assets_That_Are_Out_Of_Service(AssetStatus status)
    {
        var asset = AssetWith(status);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().AssignAsync(asset.Id, Request(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_ASSIGNABLE", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);

        // Nothing must be written for a rejected assignment.
        _assignments.Verify(r => r.AddAsync(
            It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AssetStatus.InStock)]
    [InlineData(AssetStatus.InUse)]
    [InlineData(AssetStatus.UnderMaintenance)]
    public async Task AssignAsync_Allows_In_Service_Statuses(AssetStatus status)
    {
        var asset = AssetWith(status);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);
        _assignments.Setup(r => r.AddAsync(It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().AssignAsync(asset.Id, Request(), ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCurrent);
        Assert.Equal(TargetId, result.Value.TargetId);
    }

    [Fact]
    public async Task AssignAsync_Closes_Out_The_Previous_Holder_Before_Adding_The_New_One()
    {
        var asset = AssetWith(AssetStatus.InUse);
        var incumbent = CurrentAssignment(asset.Id);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(incumbent);
        _assignments.Setup(r => r.UpdateAsync(incumbent, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assignments.Setup(r => r.AddAsync(It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().AssignAsync(asset.Id, Request(), ActorId);

        Assert.True(result.IsSuccess);

        // Two rows both claiming IsCurrent would make "who has this laptop" unanswerable.
        Assert.False(incumbent.IsCurrent);
        Assert.NotNull(incumbent.UnassignedAt);
    }

    [Fact]
    public async Task AssignAsync_Moves_An_InStock_Asset_To_InUse()
    {
        var asset = AssetWith(AssetStatus.InStock);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);
        _assignments.Setup(r => r.AddAsync(It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().AssignAsync(asset.Id, Request(), ActorId);

        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    [Fact]
    public async Task AssignAsync_Leaves_UnderMaintenance_Status_Alone()
    {
        var asset = AssetWith(AssetStatus.UnderMaintenance);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);
        _assignments.Setup(r => r.AddAsync(It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().AssignAsync(asset.Id, Request(), ActorId);

        // Only InStock is promoted. An asset out for repair that gets pre-assigned to its next
        // owner is still out for repair.
        Assert.Equal(AssetStatus.UnderMaintenance, asset.Status);
    }

    [Fact]
    public async Task AssignAsync_Records_The_Actor_As_Assigner()
    {
        var asset = AssetWith(AssetStatus.InStock);
        AssetAssignment? captured = null;

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);
        _assignments.Setup(r => r.AddAsync(It.IsAny<AssetAssignment>(), It.IsAny<CancellationToken>()))
            .Callback<AssetAssignment, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().AssignAsync(asset.Id, Request(AssignmentTargetType.Department), ActorId);

        Assert.Equal(ActorId, captured!.AssignedByUserId);
        Assert.Equal(AssignmentTargetType.Department, captured.TargetType);
        Assert.True(captured.IsCurrent);
    }

    // ---- Unassign ----------------------------------------------------------------------------

    [Fact]
    public async Task UnassignAsync_Fails_When_The_Asset_Has_No_Current_Holder()
    {
        var asset = AssetWith(AssetStatus.InStock);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);

        var result = await CreateSut().UnassignAsync(asset.Id, ActorId, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSIGNMENT_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UnassignAsync_Closes_The_Assignment()
    {
        var asset = AssetWith(AssetStatus.InUse);
        var current = CurrentAssignment(asset.Id);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        _assignments.Setup(r => r.UpdateAsync(current, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UnassignAsync(asset.Id, ActorId, "Returned to stores");

        Assert.True(result.IsSuccess);
        Assert.False(current.IsCurrent);
        Assert.NotNull(current.UnassignedAt);
        Assert.Equal("Returned to stores", current.Notes);
    }

    [Fact]
    public async Task UnassignAsync_Keeps_The_Original_Notes_When_None_Are_Supplied()
    {
        var asset = AssetWith(AssetStatus.InUse);
        var current = CurrentAssignment(asset.Id);
        current.Notes = "Original reason";

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        _assignments.Setup(r => r.UpdateAsync(current, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UnassignAsync(asset.Id, ActorId, "   ");

        // Blank notes must not wipe the history entry's existing explanation.
        Assert.Equal("Original reason", current.Notes);
    }

    [Fact]
    public async Task UnassignAsync_Does_Not_Change_The_Asset_Status()
    {
        var asset = AssetWith(AssetStatus.InUse);
        var current = CurrentAssignment(asset.Id);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        _assignments.Setup(r => r.UpdateAsync(current, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UnassignAsync(asset.Id, ActorId, null);

        // Documented asymmetry: assigning promotes InStock -> InUse, but unassigning does NOT
        // demote back. Returning an asset to stock is a lifecycle transition, done deliberately
        // through AssetLifecycleService.
        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    // ---- Reads -------------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_Returns_Null_Value_When_Unassigned()
    {
        var asset = AssetWith(AssetStatus.InStock);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetCurrentAsync(asset.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAssignment?)null);

        var result = await CreateSut().GetCurrentAsync(asset.Id);

        // "No current holder" is a successful answer, not a NotFound.
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

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
    public async Task GetHistoryAsync_Maps_Every_Assignment()
    {
        var asset = AssetWith(AssetStatus.InUse);
        var history = new[] { CurrentAssignment(asset.Id), CurrentAssignment(asset.Id) };

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assignments.Setup(r => r.GetHistoryAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(history);

        var result = await CreateSut().GetHistoryAsync(asset.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task GetByTargetAsync_Deduplicates_Assets_Assigned_More_Than_Once()
    {
        var asset = AssetWith(AssetStatus.InUse);
        asset.AssetType = new AssetType { Id = asset.AssetTypeId, Name = "Laptop" };

        // Same asset assigned to this target twice over time -- it must appear once.
        var assignments = new[]
        {
            new AssetAssignment { Id = Guid.CreateVersion7(), AssetId = asset.Id, TargetId = TargetId, IsCurrent = false },
            new AssetAssignment { Id = Guid.CreateVersion7(), AssetId = asset.Id, TargetId = TargetId, IsCurrent = true },
        };

        _assignments.Setup(r => r.GetByTargetAsync(
                AssignmentTargetType.User, TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments);
        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var result = await CreateSut().GetByTargetAsync(AssignmentTargetType.User, TargetId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Laptop", result.Value[0].AssetTypeName);

        // One lookup per distinct asset id, not per assignment row.
        _assets.Verify(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByTargetAsync_Skips_Assets_That_No_Longer_Exist()
    {
        var missingAssetId = Guid.CreateVersion7();

        _assignments.Setup(r => r.GetByTargetAsync(
                AssignmentTargetType.User, TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AssetAssignment { Id = Guid.CreateVersion7(), AssetId = missingAssetId, TargetId = TargetId },
            });
        _assets.Setup(r => r.GetByIdAsync(missingAssetId, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().GetByTargetAsync(AssignmentTargetType.User, TargetId);

        // A soft-deleted asset with a surviving assignment row must not produce a null entry.
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
