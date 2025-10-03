using Cysharp.Threading.Tasks;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;

public interface IDataRepository
{
    UniTask InitializeAsync();
    UniTask SaveChangesAsync();
}
public interface IDataRepository<T> : IDataRepository where T : IEntity, new()
{
    UniTask<T> GetByIdAsync(object id);
    UniTask<IEnumerable<T>> GetAllAsync();
    UniTask<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    UniTask InsertAsync(T entity);
    UniTask InsertMultiplesAsync(IEnumerable<T> entities);
    UniTask UpdateAsync(T entity);
    UniTask UpdateMultiplesAsync(IEnumerable<T> entities);
    UniTask DeleteAsync(T entity);
}
