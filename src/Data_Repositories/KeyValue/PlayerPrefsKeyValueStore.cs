using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerPrefsKeyValueStore : IKeyValueStore
{
    public UniTask SaveAsync<T>(string key, T data)
    {
        Save(key, data);
        return UniTask.CompletedTask;
    }
    public void Save<T>(string key, T data)
    {
        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    public UniTask<T> LoadAsync<T>(string key)
    {
        return UniTask.FromResult(Load<T>(key));
    }
    public T Load<T>(string key)
    {
        if (!HasData(key)) return default;
        var json = PlayerPrefs.GetString(key);
        return JsonUtility.FromJson<T>(json);
    }

    public bool HasData(string key)
    {
        return PlayerPrefs.HasKey(key);
    }
    public void Delete(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }
}
