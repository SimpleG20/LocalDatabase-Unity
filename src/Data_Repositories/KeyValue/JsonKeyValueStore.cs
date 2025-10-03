using System;
using System.Threading.Tasks;
using UnityEngine;

using Cysharp.Threading.Tasks;

public class JsonKeyValueStore : IKeyValueStore
{
    public async UniTask SaveAsync<T>(string key, T data)
    {
        try
        {
            var json = JsonUtility.ToJson(data);
            await System.IO.File.WriteAllTextAsync(GetFilePath(key), json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Falha ao salvar dados em {key}. Erro: {ex.Message}");
        }
    }
    public void Save<T>(string key, T data)
    {
        try
        {
            var json = JsonUtility.ToJson(data);
            System.IO.File.WriteAllText(GetFilePath(key), json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Falha ao salvar dados em {key}. Erro: {ex.Message}");
        }
    }
    public async UniTask<T> LoadAsync<T>(string key)
    {
        try
        {
            var path = GetFilePath(key);
            if (!System.IO.File.Exists(path)) return default;

            var json = await System.IO.File.ReadAllTextAsync(path);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Falha ao carregar dados de {key}. Erro: {ex.Message}");
            return default;
        }
    }
    public T Load<T>(string key)
    {
        try
        {
            var path = GetFilePath(key);
            if (!System.IO.File.Exists(path)) return default;
            var json = System.IO.File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Falha ao carregar dados de {key}. Erro: {ex.Message}");
            return default;
        }
    }

    public bool HasData(string key)
    {
        return System.IO.File.Exists(GetFilePath(key));
    }
    private string GetFilePath(string key)
    {
        return System.IO.Path.Combine(Application.persistentDataPath, $"{key}.json");
    }

    public void Delete(string key)
    {
        var path = GetFilePath(key);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }
}
