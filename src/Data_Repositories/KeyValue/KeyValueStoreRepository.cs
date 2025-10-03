using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using UnityEngine;

public class KeyValueStoreRepository<T> : IDataRepository<T> where T : IEntity, new()
{
    private readonly IKeyValueStore m_store;
    private readonly string m_collectionKey;

    private List<string> m_keys;

    public KeyValueStoreRepository(IKeyValueStore store, string collectionKey)
    {
        m_store = store;
        m_collectionKey = collectionKey;

        m_keys = new List<string>();
    }

    public async UniTask InitializeAsync()
    {
        m_keys = await m_store.LoadAsync<List<string>>($"Keys_{m_collectionKey}");
    }

    public UniTask<T> GetByIdAsync(object id)
    {
        return UniTask.FromResult(m_store.Load<T>($"{m_collectionKey}_{id}"));
    }

    public UniTask<IEnumerable<T>> GetAllAsync()
    {
        if (m_keys.Count == 0)
        {
            return UniTask.FromResult(Enumerable.Empty<T>());
        }

        return UniTask.FromResult(GetEnumerable());
    }
    public UniTask<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        if (predicate == null)
        {
            return GetAllAsync();
        }
        if (m_keys.Count == 0)
        {
            return UniTask.FromResult(Enumerable.Empty<T>());
        }
        return UniTask.FromResult(GetEnumerable(predicate));
    }
    private IEnumerable<T> GetEnumerable(Expression<Func<T, bool>> predicate = null)
    {
        var compiledPredicate = predicate?.Compile() ?? null;
        foreach (var key in m_keys)
        {
            var item = m_store.Load<T>(key);
            if (item != null && (compiledPredicate == null || compiledPredicate(item)))
            {
                yield return item;
            }
        }
    }

    public async UniTask InsertAsync(T entity)
    {
        try
        {
            await UniTask.WhenAll(
                m_store.SaveAsync($"{m_collectionKey}_{entity.Id}", entity),
                m_store.SaveAsync($"Keys_{m_collectionKey}", m_keys)
            );
            m_keys.Add($"{m_collectionKey}_{entity.Id}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to insert entity with ID {entity.Id}: {ex.Message}");
            throw;
        }
    }
    public async UniTask InsertMultiplesAsync(IEnumerable<T> entities)
    {
        var tasks = entities.Select(entity => InsertAsync(entity));
        await UniTask.WhenAll(tasks);
    }
    public async UniTask UpdateAsync(T entity)
    {
        var key = $"{m_collectionKey}_{entity.Id}";
        if (!m_store.HasData(key))
        {
            Debug.LogWarning($"Entity with ID {entity.Id} does not exist. Cannot update.");
            return;
        }
        try
        {
            await m_store.SaveAsync(key, entity);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update entity with ID {entity.Id}: {ex.Message}");
            throw;
        }
    }
    public async UniTask UpdateMultiplesAsync(IEnumerable<T> entities)
    {
        var tasks = entities.Select(entity => UpdateAsync(entity));
        await UniTask.WhenAll(tasks);
    }
    public async UniTask DeleteAsync(T entity)
    {
        var key = $"{m_collectionKey}_{entity.Id}";
        if (m_store.HasData(key))
        {
            try
            {
                m_store.Delete(key);
                m_keys.Remove(key);
                await m_store.SaveAsync($"Keys_{m_collectionKey}", m_keys);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete entity with ID {entity.Id}: {ex.Message}");
                throw;
            }
        }
    }

    public async UniTask SaveChangesAsync()
    {
        await m_store.SaveAsync($"Keys_{m_collectionKey}", m_keys);
    }
}
