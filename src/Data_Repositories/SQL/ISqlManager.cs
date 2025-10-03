using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

public interface ISqlManager : IDisposable
{
    UniTask StartDB();
    UniTask<T> FindByPrimaryKeyAsync<T>(object id) where T : IEntity, new();
    UniTask<IEnumerable<T>> GetAllAsync<T>() where T : IEntity, new();
    UniTask<IEnumerable<T>> FindAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : IEntity, new();
    UniTask<int> InsertAsync<T>(T entity) where T : IEntity, new();
    UniTask InsertMultiplesAsync<T>(IEnumerable<T> entities) where T : IEntity, new();
    UniTask UpdateAsync<T>(T entity) where T : IEntity, new();
    UniTask UpdateMultiplesAsync<T>(IEnumerable<T> entities) where T : IEntity, new();
    UniTask DeleteAsync<T>(object id) where T : IEntity, new();

    UniTask ExecuteInTransactionAsync(Action<SQLite.SQLiteConnection> operations);
    UniTask<IEnumerable<T>> QueryAsync<T>(string query, params object[] args) where T : IEntity, new();
}

