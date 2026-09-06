using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Financial.Tests;

/// <summary>
/// ContractService: lifecycle from Draft through Active to end-of-life, plus soft delete.
/// The interesting tension is that Terminated exists on the enum but nothing reaches it --
/// every path out ends in Suspended, Expired or Cancelled.
/// </summary>
public class ContractServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IContractRepository> _contracts = new(MockBehavior.Strict);

    private ContractService CreateSut() => new(_contracts.Object);

    private static Contract ContractWith(ContractStatus status = ContractStatus.Draft) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        Name = "Dell ProSupport",
        StartDate = DateTimeOffset.UtcNow,
        Status = status,
    };

    private static CreateContractRequest CreateRequest(
        string name = "Dell ProSupport",
        DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new(EntityId, name, null, null, null, start ?? DateTimeOffset.UtcNow,
            end, null, 10_000, null, null, "USD", BillingFrequency.Annual,
            false, 30, null, null, null, null, null, false);

    /// <summary>Happy-path arrange for a single-contract mutation.</summary>
    private void ArrangeMutation(Contract contract)
    {
        _contracts.Setup(r => r.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        _contracts.Setup(r => r.UpdateAsync(contract, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _contracts.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Always_Starts_The_Contract_As_Draft()
    {
        Contract? captured = null;
        _contracts.Setup(r => r.AddAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .Callback<Contract, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        _contracts.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.True(result.IsSuccess);

        // A contract becomes a financial obligation the moment it is Active, so it must never be
        // born in that state -- the review step is the whole point of Draft.
        Assert.Equal(ContractStatus.Draft, captured!.Status);
        Assert.Equal(EntityId, captured.EntityId);
        Assert.Equal(7, captured.Id.Version); // UUIDv7, not v4
    }

    [Fact]
    public async Task CreateAsync_Persists_The_Commercial_Terms()
    {
        Contract? captured = null;
        _contracts.Setup(r => r.AddAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .Callback<Contract, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        _contracts.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.Equal(10_000, captured!.Value);
        Assert.Equal(30, captured.NoticePeriodDays);
        Assert.Equal(BillingFrequency.Annual, captured.BillingFrequency);
        Assert.Equal(ActorId, captured.CreatedById);
    }

    // ---- Status machine ----------------------------------------------------------------------

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.Active)]
    [InlineData(ContractStatus.Active, ContractStatus.Suspended)]
    [InlineData(ContractStatus.Active, ContractStatus.Expired)]
    [InlineData(ContractStatus.Active, ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Suspended, ContractStatus.Active)]
    [InlineData(ContractStatus.Suspended, ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Expired, ContractStatus.Active)] // renewal
    public async Task ChangeStatusAsync_Allows_The_Documented_Transitions(
        ContractStatus from, ContractStatus to)
    {
        var contract = ContractWith(from);
        ArrangeMutation(contract);

        var result = await CreateSut().ChangeStatusAsync(contract.Id, to, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, contract.Status);
        Assert.Equal(ActorId, contract.UpdatedById);
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.Suspended)] // skip review
    [InlineData(ContractStatus.Draft, ContractStatus.Expired)]
    [InlineData(ContractStatus.Draft, ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Active, ContractStatus.Terminated)]   // no path exists
    [InlineData(ContractStatus.Suspended, ContractStatus.Expired)]
    [InlineData(ContractStatus.Suspended, ContractStatus.Terminated)]
    [InlineData(ContractStatus.Expired, ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Cancelled, ContractStatus.Active)]
    [InlineData(ContractStatus.Terminated, ContractStatus.Active)]
    public async Task ChangeStatusAsync_Rejects_Undocumented_And_Terminal_Paths(
        ContractStatus from, ContractStatus to)
    {
        var contract = ContractWith(from);

        _contracts.Setup(r => r.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

        var result = await CreateSut().ChangeStatusAsync(contract.Id, to, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.INVALID_TRANSITION", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(from, contract.Status);

        _contracts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_Rejected_Terminated_Is_Dead_End_By_Design()
    {
        var contract = ContractWith(ContractStatus.Active);

        _contracts.Setup(r => r.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

        var result = await CreateSut().ChangeStatusAsync(contract.Id, ContractStatus.Terminated, ActorId);

        // Terminated exists on the enum yet no valid transition produces it. That is intentional:
        // termination is a legal/billing outcome recorded elsewhere, and stuffing it into this
        // machine would let a finance clerk retire a contract that legal still enforces.
        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.INVALID_TRANSITION", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_Rejects_A_No_Op_Transition()
    {
        var contract = ContractWith(ContractStatus.Active);

        _contracts.Setup(r => r.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

        var result = await CreateSut().ChangeStatusAsync(contract.Id, ContractStatus.Active, ActorId);

        // Its own code, distinct from INVALID_TRANSITION: a no-op is not a forbidden path, it is
        // the absence of a transition. Without it the call would write an audit-less no-change row.
        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.STATUS_UNCHANGED", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_Returns_NotFound_For_A_Missing_Contract()
    {
        var id = Guid.CreateVersion7();
        _contracts.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Contract?)null);

        var result = await CreateSut().ChangeStatusAsync(id, ContractStatus.Active, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.NOT_FOUND", result.Error!.Code);
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Cannot_Change_Status()
    {
        var contract = ContractWith(ContractStatus.Active);
        ArrangeMutation(contract);

        await CreateSut().UpdateAsync(contract.Id,
            new UpdateContractRequest("Renamed", null, null, null, contract.StartDate, null, null,
                10_000, null, null, "USD", BillingFrequency.Quarterly, false, 30, null, null, null, false),
            ActorId);

        // Status moves only through ChangeStatusAsync, which validates the transition. The edit
        // payload must not become a back door around the state machine.
        Assert.Equal(ContractStatus.Active, contract.Status);
        Assert.Equal(BillingFrequency.Quarterly, contract.BillingFrequency);
    }

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Contract()
    {
        var id = Guid.CreateVersion7();
        _contracts.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Contract?)null);

        var result = await CreateSut().UpdateAsync(id,
            new UpdateContractRequest("X", null, null, null, DateTimeOffset.UtcNow, null, null,
                null, null, null, "USD", BillingFrequency.Monthly, false, null, null, null, null, false),
            ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.NOT_FOUND", result.Error!.Code);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletes_And_Keeps_The_Status()
    {
        var contract = ContractWith(ContractStatus.Suspended);
        ArrangeMutation(contract);

        var result = await CreateSut().DeleteAsync(contract.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(contract.IsDeleted);
        Assert.NotNull(contract.DeletedAt);

        // A soft delete hides the row; it must not also rewrite history by resetting the status.
        Assert.Equal(ContractStatus.Suspended, contract.Status);
        Assert.Equal(ActorId, contract.UpdatedById);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_Contract()
    {
        var id = Guid.CreateVersion7();
        _contracts.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Contract?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.NOT_FOUND", result.Error!.Code);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Contract()
    {
        var id = Guid.CreateVersion7();
        _contracts.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Contract?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTRACT.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetPagedAsync_Passes_Every_Filter_Through()
    {
        var supplierId = Guid.CreateVersion7();

        _contracts.Setup(r => r.GetPagedAsync(
                EntityId, "dell", ContractStatus.Active, supplierId, null, 2, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Contract>(), 0));

        var result = await CreateSut().GetPagedAsync(
            EntityId, "dell", ContractStatus.Active, supplierId, null, 2, 50);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
    }
}