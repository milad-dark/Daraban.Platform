using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Financial.Tests;

/// <summary>
/// Purchases are the sharpest surface in Financial: money changes state, and the status machine
/// plus the item-total recalculation are what keep the books consistent. The tests pin the
/// transition matrix, the ApprovalById stamping, and that Add/Remove re-sum the totals from the
/// actual line items rather than trusting whatever number the client sent.
/// </summary>
public class PurchaseServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IPurchaseRepository> _purchases = new(MockBehavior.Strict);

    private PurchaseService CreateSut() => new(_purchases.Object);

    private static Purchase PurchaseWith(
        PurchaseStatus status = PurchaseStatus.Draft, decimal total = 0, decimal tax = 0) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        OrderNumber = "PO-2026-001",
        Name = "Ten laptops",
        Status = status,
        TotalAmount = total,
        TaxAmount = tax,
        RequestedById = ActorId,
        RequestedDate = DateTimeOffset.UtcNow,
        Items = new List<PurchaseItem>(),
    };

    private static PurchaseItem Item(decimal qty = 1, decimal unit = 100, decimal disc = 0, decimal tax = 20)
        => new() { Id = Guid.CreateVersion7(), PurchaseId = Guid.CreateVersion7(), Quantity = (int)qty, UnitPrice = unit, DiscountPercent = disc, TaxRate = tax };

    private static CreatePurchaseRequest CreateRequest(string orderNumber = "PO-2026-001")
        => new(EntityId, orderNumber, "Ten laptops", null, null, 1200, 240, "USD", null, "Net 30", null, null, null);

    /// <summary>Arrange the details fetch + save for mutations on a purchase.</summary>
    private void ArrangeDetails(Purchase purchase)
    {
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        _purchases.Setup(r => r.UpdateAsync(purchase, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    /// <summary>Arrange the light fetch used by ChangeStatus/Delete (which only mutate the header).</summary>
    private void ArrangeHeader(Purchase purchase)
    {
        _purchases.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _purchases.Setup(r => r.UpdateAsync(purchase, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Order_Number()
    {
        _purchases.Setup(r => r.OrderNumberExistsAsync("PO-2026-001", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.ORDER_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _purchases.Verify(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Opens_A_Draft_And_Records_The_Requester()
    {
        Purchase? captured = null;
        _purchases.Setup(r => r.OrderNumberExistsAsync("PO-2026-001", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _purchases.Setup(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Callback<Purchase, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseStatus.Draft, captured!.Status);
        Assert.Equal(ActorId, captured.RequestedById);
        Assert.Equal(7, captured.Id.Version);
        Assert.NotNull(captured.RequestedDate);
    }

    // ---- Status machine ----------------------------------------------------------------------

    [Theory]
    [InlineData(PurchaseStatus.Draft, PurchaseStatus.PendingApproval)]
    [InlineData(PurchaseStatus.PendingApproval, PurchaseStatus.Approved)]
    [InlineData(PurchaseStatus.PendingApproval, PurchaseStatus.Cancelled)]
    [InlineData(PurchaseStatus.Approved, PurchaseStatus.Ordered)]
    [InlineData(PurchaseStatus.Approved, PurchaseStatus.Cancelled)]
    [InlineData(PurchaseStatus.Ordered, PurchaseStatus.PartiallyReceived)]
    [InlineData(PurchaseStatus.Ordered, PurchaseStatus.Received)]
    [InlineData(PurchaseStatus.PartiallyReceived, PurchaseStatus.Received)]
    public async Task ChangeStatusAsync_Allows_The_Documented_Transitions(PurchaseStatus from, PurchaseStatus to)
    {
        var purchase = PurchaseWith(from);
        ArrangeHeader(purchase);

        var result = await CreateSut().ChangeStatusAsync(purchase.Id, to, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, purchase.Status);
    }

    [Theory]
    [InlineData(PurchaseStatus.Draft, PurchaseStatus.Approved)]        // must pass through pending
    [InlineData(PurchaseStatus.Draft, PurchaseStatus.Received)]
    [InlineData(PurchaseStatus.PendingApproval, PurchaseStatus.Ordered)]
    [InlineData(PurchaseStatus.Approved, PurchaseStatus.Received)]     // can't skip ordering
    [InlineData(PurchaseStatus.Received, PurchaseStatus.Draft)]        // terminal, no rewinds
    [InlineData(PurchaseStatus.Cancelled, PurchaseStatus.Draft)]       // terminal, no rewinds
    [InlineData(PurchaseStatus.Received, PurchaseStatus.PartiallyReceived)]
    public async Task ChangeStatusAsync_Rejects_Skipped_And_Terminal_Transitions(PurchaseStatus from, PurchaseStatus to)
    {
        var purchase = PurchaseWith(from);
        _purchases.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().ChangeStatusAsync(purchase.Id, to, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.INVALID_TRANSITION", result.Error!.Code);
        Assert.Equal(from, purchase.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_Stamps_Approval_With_Who_Approved()
    {
        var purchase = PurchaseWith(PurchaseStatus.PendingApproval);
        ArrangeHeader(purchase);

        var result = await CreateSut().ChangeStatusAsync(purchase.Id, PurchaseStatus.Approved, ActorId);

        // An approval ledger that says when but not who is not an audit trail.
        Assert.True(result.IsSuccess);
        Assert.NotNull(purchase.ApprovedDate);
        Assert.Equal(ActorId, purchase.ApprovedById);
    }

    [Fact]
    public async Task ChangeStatusAsync_Stamps_The_Ordered_And_Received_Dates()
    {
        var approx = DateTimeOffset.UtcNow;

        var ordered = PurchaseWith(PurchaseStatus.Approved);
        ArrangeHeader(ordered);
        await CreateSut().ChangeStatusAsync(ordered.Id, PurchaseStatus.Ordered, ActorId);
        Assert.NotNull(ordered.OrderedDate);
        Assert.InRange(ordered.OrderedDate!.Value, approx.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));

        var received = PurchaseWith(PurchaseStatus.PartiallyReceived);
        ArrangeHeader(received);
        await CreateSut().ChangeStatusAsync(received.Id, PurchaseStatus.Received, ActorId);
        Assert.NotNull(received.ReceivedDate);
    }

    [Fact]
    public async Task ChangeStatusAsync_Rejects_A_No_Op()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        _purchases.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().ChangeStatusAsync(purchase.Id, PurchaseStatus.Draft, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.INVALID_TRANSITION", result.Error!.Code);
    }

    // ---- Update/Delete guards -----------------------------------------------------------------

    [Theory]
    [InlineData(PurchaseStatus.Approved)]
    [InlineData(PurchaseStatus.Ordered)]
    [InlineData(PurchaseStatus.Received)]
    [InlineData(PurchaseStatus.Cancelled)]
    public async Task UpdateAsync_Refuses_Frozen_Statuses(PurchaseStatus status)
    {
        var purchase = PurchaseWith(status);
        _purchases.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().UpdateAsync(
            purchase.Id,
            new UpdatePurchaseRequest("Renamed", null, null, 0, 0, "USD", null, null, null, null, null),
            ActorId);

        // Once money has moved, editing the numbers is an accounting change, not an edit.
        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.UPDATE_BLOCKED", result.Error!.Code);
    }

    [Theory]
    [InlineData(PurchaseStatus.PendingApproval)]
    [InlineData(PurchaseStatus.Approved)]
    [InlineData(PurchaseStatus.Ordered)]
    [InlineData(PurchaseStatus.Received)]
    public async Task DeleteAsync_Only_Allows_Drafts(PurchaseStatus status)
    {
        var purchase = PurchaseWith(status);
        _purchases.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().DeleteAsync(purchase.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.DELETE_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_A_Draft()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        ArrangeHeader(purchase);

        var result = await CreateSut().DeleteAsync(purchase.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(purchase.IsDeleted);
    }

    // ---- Line items --------------------------------------------------------------------------

    [Fact]
    public async Task AddItemAsync_Requires_A_Draft()
    {
        var purchase = PurchaseWith(PurchaseStatus.Approved);
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().AddItemAsync(purchase.Id,
            new CreatePurchaseItemRequest("Laptop", null, 1, 1000, 0, 20, null, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.ADD_ITEM_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task AddItemAsync_Recomputes_Total_And_Tax_From_The_Items()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        purchase.TotalAmount = 999; // stale value the client had sent at creation
        purchase.TaxAmount = 999;
        var now = DateTimeOffset.UtcNow;
        PurchaseItem? captured = null;

        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _purchases.Setup(r => r.UpdateAsync(purchase, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // 2 laptops at 1,000 = 2,000; 20% tax = 400.
        var result = await CreateSut().AddItemAsync(purchase.Id,
            new CreatePurchaseItemRequest("Laptop", null, 2, 1000, 0, 20, null, null), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Single(purchase.Items);
        captured = purchase.Items.Single();
        Assert.Equal(2000m, purchase.TotalAmount);
        Assert.Equal(400m, purchase.TaxAmount);
        Assert.Equal(2400m, purchase.TotalWithTax);
        Assert.Equal(now.Date, purchase.UpdatedAt.Date);
    }

    [Fact]
    public async Task AddItemAsync_Applies_Discount_And_Tax_Per_Line()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _purchases.Setup(r => r.UpdateAsync(purchase, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // 10 units at 100 with 10% discount = 900; 20% tax = 180.
        await CreateSut().AddItemAsync(purchase.Id,
            new CreatePurchaseItemRequest("Cables", null, 10, 100, 10, 20, null, null), ActorId);

        var item = purchase.Items.Single();
        Assert.Equal(900m, item.LineTotal);
        Assert.Equal(180m, item.TaxAmount);
        Assert.Equal(1080m, item.TotalWithTax);
        Assert.Equal(900m, purchase.TotalAmount);
        Assert.Equal(180m, purchase.TaxAmount);
    }

    [Fact]
    public async Task RemoveItemAsync_Recomputes_After_Removal()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        purchase.Items = new List<PurchaseItem>
        {
            Item(qty: 2, unit: 1000, tax: 20),  // subtotal 2000, tax 400
            Item(qty: 1, unit: 500, tax: 20),   // subtotal 500, tax 100
        };
        purchase.TotalAmount = 2500;
        purchase.TaxAmount = 500;
        var target = purchase.Items.Skip(1).First();

        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _purchases.Setup(r => r.UpdateAsync(purchase, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _purchases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().RemoveItemAsync(purchase.Id, target.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Single(purchase.Items);
        Assert.Equal(2000m, purchase.TotalAmount);
        Assert.Equal(400m, purchase.TaxAmount);
    }

    [Fact]
    public async Task RemoveItemAsync_Returns_NotFound_For_A_Unknown_Item()
    {
        var purchase = PurchaseWith(PurchaseStatus.Draft);
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().RemoveItemAsync(purchase.Id, Guid.CreateVersion7(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.ITEM_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveItemAsync_Requires_A_Draft()
    {
        var purchase = PurchaseWith(PurchaseStatus.Received);
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().RemoveItemAsync(purchase.Id, Guid.CreateVersion7(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.REMOVE_ITEM_BLOCKED", result.Error!.Code);
    }

    // ---- Reads ------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Exposes_Total_With_Tax()
    {
        var purchase = PurchaseWith(status: PurchaseStatus.Draft, total: 1200, tax: 240);
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);

        var result = await CreateSut().GetByIdAsync(purchase.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1440m, result.Value.TotalWithTax); // 1200 + 240
        Assert.Equal("PO-2026-001", result.Value.OrderNumber);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Purchase()
    {
        var id = Guid.CreateVersion7();
        _purchases.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Purchase?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("PURCHASE.NOT_FOUND", result.Error!.Code);
    }
}