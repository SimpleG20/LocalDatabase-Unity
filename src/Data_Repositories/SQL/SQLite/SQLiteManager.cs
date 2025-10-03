using Cysharp.Threading.Tasks;
using SQLite;
using System;
using System.IO;
using System.Threading;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

public class SQLiteManager : ISqlManager
{
    // Name of the DB file in StreamingAssets and the final name in persistentDataPath.
    private const string DB_FILENAME = "game.db";
    private const int MAX_POOL_SIZE = 10;
    private const int MIN_POOL_SIZE = 2;

    public string DbPath { get; private set; }

    private readonly ConcurrentQueue<SQLiteConnection> m_connectionPool;
    private readonly SemaphoreSlim m_poolSemaphore;

    public SQLiteManager()
    {
        DbPath = Path.Combine(Application.persistentDataPath, DB_FILENAME).Replace('\\', '/');
        m_connectionPool = new();
        m_poolSemaphore = new SemaphoreSlim(MIN_POOL_SIZE, MAX_POOL_SIZE);
    }

    public async UniTask StartDB()
    {
        try
        {
            // Ensure the database file exists in persistentDataPath. If not, copy it from StreamingAssets.
            if (!File.Exists(DbPath))
            {
                try
                {
                    // The copy is asynchronous because on Android we must use UnityWebRequest to read from the APK.
                    await CopyDbFromStreamingAssetsAsync();
                }
                catch (FileNotFoundException ex)
                {
                    // Failsafe: If the database doesn't exist in StreamingAssets, create an empty one
                    Debug.LogWarning($"Database not found in StreamingAssets. Creating a new empty database: {ex.Message}");

                    // Create directory if it doesn't exist
                    string dbDirectory = Path.GetDirectoryName(DbPath);
                    if (!Directory.Exists(dbDirectory) && !string.IsNullOrEmpty(dbDirectory))
                    {
                        Directory.CreateDirectory(dbDirectory);
                    }

                    // Create an empty SQLite file with optimized settings
                    await UniTask.RunOnThreadPool(() =>
                    {
                        using var conn = CreateOptimizedConnection();
                        // Just opening and closing the connection creates the empty file
                    });
                    Debug.Log($"Empty database created at: {DbPath}");
                }
            }
            else
            {
                Debug.Log($"DB already present at: {DbPath}");
            }

            await InitializeConnectionPoolAsync();

            Debug.Log("SQLiteManager initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing database: {ex.Message}");
        }
    }
    private async UniTask CopyDbFromStreamingAssetsAsync()
    {
        string sourcePath;

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, streaming assets are inside the APK as a compressed asset; we must read via UnityWebRequest.
        sourcePath = Path.Combine(Application.streamingAssetsPath, DbFileName);
        using (UnityWebRequest uwr = UnityWebRequest.Get(sourcePath))
        {
            uwr.downloadHandler = new DownloadHandlerBuffer();
            await uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Failed to read DB from StreamingAssets: {uwr.error}");
                throw new Exception($"Failed to read DB from StreamingAssets: {uwr.error}");
            }

            byte[] data = uwr.downloadHandler.data;
            await UniTask.RunOnThreadPool(() => File.WriteAllBytes(DbPath, data));
        }
#else
        // In iOS, Windows, macOS and Editor, StreamingAssets files are regular files that we can copy directly.
        sourcePath = Path.Combine(Application.streamingAssetsPath, DB_FILENAME);
        if (!File.Exists(sourcePath))
        {
            Debug.LogError($"StreamingAssets DB not found at: {sourcePath}");
            throw new FileNotFoundException("Missing StreamingAssets DB", sourcePath);
        }

        // Using FileStream for better copy performance with larger buffer
        await UniTask.RunOnThreadPool(() =>
        {
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            using var destStream = new FileStream(DbPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough);
            sourceStream.CopyTo(destStream, 65536);
        });
#endif
    }
    private SQLiteConnection CreateOptimizedConnection()
    {
        var conn = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, true)
        {
            BusyTimeout = TimeSpan.FromSeconds(10)
        };

        return conn;
    }
    private async UniTask InitializeConnectionPoolAsync()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            // Pre-populate the connection pool with minimum connections
            for (int i = 0; i < MIN_POOL_SIZE; i++)
            {
                var conn = CreateOptimizedConnection();
                m_connectionPool.Enqueue(conn);
            }
        });
    }


    /// <summary>
    /// Optimized single item query
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="primaryKey"></param>
    /// <returns></returns>
    public async UniTask<T> FindByPrimaryKeyAsync<T>(object primaryKey) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() => connection.Find<T>(primaryKey));
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    public async UniTask<IEnumerable<T>> FindAsync<T>(Expression<Func<T, bool>> predicate) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() => connection.Table<T>().Where(predicate).ToList());
        }
        finally
        {
            ReturnConnection(connection);
        }
    }
    /// <summary>
    /// Optimized table query operation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async UniTask<IEnumerable<T>> GetAllAsync<T>() where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() => connection.Table<T>());
        }
        finally
        {
            ReturnConnection(connection);
        }
    }


    /// <summary>
    /// Optimized single insert operation - now returns the actual inserted ID
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="item"></param>
    /// <returns></returns>
    public async UniTask<int> InsertAsync<T>(T item) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                connection.Insert(item);
                // Get the ID of the last inserted row
                return (int)SQLite3.LastInsertRowid(connection.Handle);
            });
        }
        finally
        {
            ReturnConnection(connection);
        }
    }
    /// <summary>
    /// High-performance batch insert operation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    public async UniTask InsertMultiplesAsync<T>(IEnumerable<T> items) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            await UniTask.RunOnThreadPool(() =>
            {
                connection.RunInTransaction(() =>
                {
                    connection.InsertAll(items);
                });
            });
        }
        finally
        {
            ReturnConnection(connection);
        }
    }


    /// <summary>
    /// Optimized single update operation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="item"></param>
    /// <returns></returns>
    public async UniTask UpdateAsync<T>(T item) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            await UniTask.RunOnThreadPool(() => connection.Update(item));
        }
        finally
        {
            ReturnConnection(connection);
        }
    }
    /// <summary>
    /// High-performance batch update operation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    public async UniTask UpdateMultiplesAsync<T>(IEnumerable<T> items) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            await UniTask.RunOnThreadPool(() =>
            {
                connection.RunInTransaction(() =>
                {
                    connection.UpdateAll(items);
                });
            });
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    /// <summary>
    /// Optimized single delete operation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="primaryKey"></param>
    /// <returns></returns>
    public async UniTask DeleteAsync<T>(object primaryKey) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            await UniTask.RunOnThreadPool(() => connection.Delete<T>(primaryKey));
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    /// <summary>
    /// Optimized query operation with connection pooling
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sql"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public async UniTask<IEnumerable<T>> QueryAsync<T>(string sql, params object[] args) where T : IEntity, new()
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() => connection.Query<T>(sql, args));
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    /// <summary>
    /// Generic method for executing operations without return value in the database
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public async UniTask<T> ExecuteDbOperationAsync<T>(Func<SQLiteConnection, T> operation)
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await UniTask.RunOnThreadPool(() => operation(connection));
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    private async UniTask<SQLiteConnection> GetConnectionAsync()
    {
        await m_poolSemaphore.WaitAsync();

        if (m_connectionPool.TryDequeue(out var connection))
        {
            // Verify connection is still valid
            try
            {
                connection.Execute("SELECT 1");
                return connection;
            }
            catch
            {
                // Connection is invalid, create a new one
                connection?.Dispose();
                return CreateOptimizedConnection();
            }
        }

        // Pool is empty, create new connection
        return CreateOptimizedConnection();
    }
    private void ReturnConnection(SQLiteConnection connection)
    {
        try
        {
            if (connection != null && m_connectionPool.Count < MAX_POOL_SIZE)
            {
                m_connectionPool.Enqueue(connection);
            }
            else
            {
                connection?.Dispose();
            }
        }
        finally
        {
            m_poolSemaphore.Release();
        }
    }

    /// <summary>
    /// Transaction support for better performance with multiple operations
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public async UniTask ExecuteInTransactionAsync(Action<SQLiteConnection> operations)
    {
        var connection = await GetConnectionAsync();
        try
        {
            await UniTask.RunOnThreadPool(() =>
            {
                connection.RunInTransaction(() => operations(connection));
            });
        }
        finally
        {
            ReturnConnection(connection);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Optimize database when app is paused
            OptimizeAsync().Forget();
        }
    }
    private async UniTask OptimizeAsync()
    {
        await ExecuteDbOperationAsync<object>(conn =>
        {
            conn.Execute("VACUUM");
            conn.Execute("ANALYZE");
            conn.Execute("PRAGMA optimize");
            return null;
        });
    }

    public void Dispose()
    {
        Cleanup();
    }
    private void Cleanup()
    {
        // Dispose all connections in pool
        while (m_connectionPool.TryDequeue(out var connection))
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error disposing connection: {ex.Message}");
            }
        }
    }
}