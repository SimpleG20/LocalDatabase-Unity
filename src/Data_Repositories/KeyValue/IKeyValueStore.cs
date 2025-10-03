using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

public interface IKeyValueStore
{
    void Save<T>(string key, T data);
    T Load<T>(string key);  

    UniTask SaveAsync<T>(string key, T data);  
    UniTask<T> LoadAsync<T>(string key);  

    void Delete(string key);
    bool HasData(string key);
}
