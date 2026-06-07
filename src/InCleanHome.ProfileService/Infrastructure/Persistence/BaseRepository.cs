using InCleanHome.ProfileService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ProfileService.Infrastructure.Persistence;

public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly ProfileDbContext Context;

    protected BaseRepository(ProfileDbContext context)
    {
        Context = context;
    }

    public async Task AddAsync(TEntity entity)         => await Context.Set<TEntity>().AddAsync(entity);
    public async Task<TEntity?> FindByIdAsync(int id)  => await Context.Set<TEntity>().FindAsync(id);
    public void Update(TEntity entity)                 => Context.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity)                 => Context.Set<TEntity>().Remove(entity);
    public async Task<IEnumerable<TEntity>> ListAsync()=> await Context.Set<TEntity>().ToListAsync();
}

public class UnitOfWork(ProfileDbContext context) : IUnitOfWork
{
    public async Task CompleteAsync() => await context.SaveChangesAsync();
}
