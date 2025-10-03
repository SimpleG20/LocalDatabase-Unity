using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

public class SqliteRepository<T> : ISqlRepository<T> where T : IEntity, new()
{
    private readonly ISqlManager m_manager;

    public SqliteRepository(ISqlManager sqlManager)
    {
        m_manager = sqlManager;
    }

    public async UniTask InitializeAsync()
    {
        try
        {
            await m_manager.StartDB();
        }
        catch
        {
            throw;
        }
    }

    public UniTask<T> GetByIdAsync(object id)
    {
        return m_manager.FindByPrimaryKeyAsync<T>(id);
    }
    public UniTask<IEnumerable<T>> GetAllAsync()
    {
        return m_manager.GetAllAsync<T>();
    }
    public UniTask<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        if (predicate == null)
        {
            return GetAllAsync();
        }

        return m_manager.FindAsync(predicate);
    }

    public UniTask InsertAsync(T entity)
    {
        return m_manager.InsertAsync(entity);
    }
    public UniTask InsertMultiplesAsync(IEnumerable<T> entities)
    {
        return m_manager.InsertMultiplesAsync(entities);
    }

    public UniTask UpdateAsync(T entity)
    {
        return m_manager.UpdateAsync(entity);
    }
    public UniTask UpdateMultiplesAsync(IEnumerable<T> entities)
    {
        return m_manager.UpdateMultiplesAsync(entities);
    }

    public UniTask DeleteAsync(T entity)
    {
        return m_manager.DeleteAsync<T>(entity.Id);
    }

    public UniTask<IEnumerable<T>> ExecuteQuery(string query, params object[] args)
    {
        return m_manager.QueryAsync<T>(query, args);
    }
    public UniTask ExecuteInTransactionAsync(Action<SQLite.SQLiteConnection> operations)
    {
        return m_manager.ExecuteInTransactionAsync(operations);
    }

    public UniTask SaveChangesAsync()
    {
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        m_manager?.Dispose();
    }
}