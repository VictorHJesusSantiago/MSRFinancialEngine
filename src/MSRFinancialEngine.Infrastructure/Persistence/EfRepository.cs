using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Application.Abstractions;

namespace MSRFinancialEngine.Infrastructure.Persistence;

public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly FinancialEngineDbContext _context;
    private readonly DbSet<T> _set;

    public EfRepository(FinancialEngineDbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public IQueryable<T> Query() => _set.AsQueryable();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => await _set.FindAsync([id], ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}

public class EfUnitOfWork : IUnitOfWork
{
    private readonly FinancialEngineDbContext _context;

    public EfUnitOfWork(FinancialEngineDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
