using Flippo.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flippo.Data.Services;

/// <summary>Fachlicher Store für SessionRecords.</summary>
public sealed class SessionStore
{
    private readonly IDbContextFactory<FlippoDbContext> _factory;

    public SessionStore(IDbContextFactory<FlippoDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<SessionRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var list = await db.SessionRecords.AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);
        return list.Select(r => r.ToDomain()).ToList();
    }

    public async Task<long> AddAsync(SessionRecord record, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = record.ToEntity();
        e.Id = 0;
        db.SessionRecords.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }
}
