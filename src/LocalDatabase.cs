using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class LocalDatabase : IDisposable
{
    public static LocalDatabase Instance { get; private set; }

    private Dictionary<Type, IDataRepository> m_repositories = new Dictionary<Type, IDataRepository>();

    public LocalDatabase()
    {
        if (Instance != null)
        {
            throw new Exception("LocalDatabase instance already exists!");
        }
        Instance = this;
    }

    public LocalDatabase RegisterRepository<T>(IDataRepository<T> repository) where T : IEntity, new()
    {
        if (repository == null) throw new ArgumentNullException(nameof(repository));

        m_repositories.TryAdd(typeof(T), repository);
        return this;
    }

    public async UniTask InitializeRepositoriesAsync()
    {
        try
        {
            var tasks = m_repositories.Values.Select(repo => repo.InitializeAsync());
            await UniTask.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing repositories: {ex.Message}");
        }
    }

    public IDataRepository<T> GetRepository<T>() where T : IEntity, new()
    {
        if (m_repositories.TryGetValue(typeof(T), out var repo))
        {
            return repo as IDataRepository<T>;
        }

        throw new Exception($"Repository for type {typeof(T).Name} not found.");
    }

    public void Dispose()
    {
        foreach (var repo in m_repositories.Values)
        {
            if (repo is IDisposable disposableRepo)
            {
                disposableRepo.Dispose();
            }
        }
        m_repositories.Clear();
        Instance = null;
    }
}
