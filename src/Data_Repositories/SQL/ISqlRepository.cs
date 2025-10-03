using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

public interface ISqlRepository<T> : IDataRepository<T>, IDisposable
    where T : IEntity, new()
{
    UniTask<IEnumerable<T>> ExecuteQuery(string query, params object[] args);
    UniTask ExecuteInTransactionAsync(Action<SQLite.SQLiteConnection> operations);
}