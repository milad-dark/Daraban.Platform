using Daraban.Modules.Inventory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Inventory.Data.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _db;

    public InventoryRepository(InventoryDbContext db) => _db = db;

    public void Add(RawInventorySubmission submission)
        => _db.RawInventorySubmissions.Add(submission);

    public async Task<RawInventorySubmission?> GetByHashAsync(string hash, Guid agentId, CancellationToken ct = default)
        => await _db.RawInventorySubmissions
            .FirstOrDefaultAsync(x => x.SubmissionHash == hash && x.AgentId == agentId, ct);

    public async Task<RawInventorySubmission?> GetByIdAsync(long id, Guid agentId, CancellationToken ct = default)
        => await _db.RawInventorySubmissions
            .FirstOrDefaultAsync(x => x.Id == id && x.AgentId == agentId, ct);

    public async Task<IReadOnlyList<RawInventorySubmission>> ListAsync(
        Guid agentId, int skip, int take, CancellationToken ct = default)
        => await _db.RawInventorySubmissions
            .Where(x => x.AgentId == agentId)
            .OrderByDescending(x => x.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<int> GetCountAsync(Guid agentId, CancellationToken ct = default)
        => await _db.RawInventorySubmissions
            .CountAsync(x => x.AgentId == agentId, ct);

    public async Task<RawInventorySubmission?> GetByIdUnscopedAsync(long id, CancellationToken ct = default)
        => await _db.RawInventorySubmissions
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public void Update(RawInventorySubmission submission)
        => _db.RawInventorySubmissions.Update(submission);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
