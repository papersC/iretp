using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Data;

namespace IRETP.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly IretpDbContext _context;

    public UnitOfWork(IretpDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
