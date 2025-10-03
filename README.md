# Flexible Data Persistence System for Unity (v1.0.0)

This is a robust and decoupled data persistence system for Unity, designed with **SOLID** principles and **Design Patterns** in mind. The goal is to provide an abstraction layer that allows game data to be saved and loaded consistently, regardless of the underlying storage mechanism (local database, JSON files, PlayerPrefs, etc.).

## Architecture

The system is designed to be modular and extensible. The data flow follows a clear separation of responsibilities, now with a dedicated path for high-performance SQL backends.

```
                                                   +-------------------------+
                                                   | ISqlManager             |
                                                   | (e.g., SQLiteManager)   |
                                                   +------------+------------+
                                                                ^
                                                                |
[Game Logic (GameManager)] <--> [LocalDatabase (Service Locator)] <--> [IDataRepository<T>] --+--> [ISqlRepository<T>] -----+
                                                                                                 |
                                                                                                 +--> [KeyValueStoreRepository<T>] <--> [IKeyValueStore] <--> [Final Mechanism (JSON, PlayerPrefs)]
```

### Dependencies
The system relies on the following open-source libraries:
  * [Cysharp/UniTask](https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask): For asynchronous programming in Unity.
  * [sqlite-net-pcl](https://github.com/gilzoide/unity-sqlite-net.git): A lightweight ADO.NET provider for SQLite.

So, make sure to install these packages via the Unity Package Manager or by adding them to your project.

### Core Components

  * **`IEntity`**: A base interface for all data models that will be saved. It ensures that each entity has an `Id` property, essential for its unique identification.
  * **`IDataRepository<T>`**: The main interface that the game logic interacts with. It defines a contract for CRUD (Create, Read, Update, Delete) and query operations, now fully asynchronous (using `UniTask`).
  * **`ISqlRepository<T>`**: A specialization of `IDataRepository` that adds functionalities specific to SQL-based backends, such as executing direct queries and atomic transactions (`ExecuteInTransactionAsync`).
  * **`LocalDatabase`**: Acts as a **Service Locator**. It is a central and unique access point (`Instance`) for obtaining any repository registered in the system (e.g., `GetRepository<PlayerData>()`). It now manages the asynchronous initialization of all repositories.
  * **`ISqlManager`**: A new abstraction for the low-level database manager (e.g., `SQLiteManager`). It manages the connection pool, the execution of operations on separate threads, and the database initialization, ensuring high performance and preventing the game from freezing.
  * **`IKeyValueStore`**: An abstraction for the key-value storage mechanism. It defines basic methods like `Save`, `Load`, and `Delete`, implementing the **Strategy** pattern.
  * **`SqliteRepository<T>`**: A concrete and high-performance implementation that uses an `ISqlManager` to execute operations directly on the SQLite database.
  * **`KeyValueStoreRepository<T>`**: An **Adapter** pattern implementation that "adapts" the simple interface of an `IKeyValueStore` to behave like a full `IDataRepository<T>`.

### The "Key Index" Pattern

The `KeyValueStoreRepository` implementation uses a "key index" pattern to manage data collections:

1.  Each entity is saved in its own file/key (e.g., `"player_data_1.json"`).
2.  A list containing all keys in the collection is maintained in a separate index file/key (e.g., `"Keys_player_data.json"`).

**Advantages:**

  * **Fast individual operations**: `GetByIdAsync`, `UpdateAsync`, and `DeleteAsync` are very efficient.
  * **Scalability**: Handles a large number of entities better than a single monolithic JSON file.

**Disadvantage:**

  * **Slower batch operations**: `GetAllAsync` and `FindAsync` need to perform multiple disk reads.

## How to Use

Using the system in your game is simple and straightforward, divided into three steps.

### 1\. Defining Your Data

Create a class for your data and ensure it implements the `IEntity` interface.

```csharp
// Example: Player Data
using SQLite;

public class PlayerData : IEntity
{
    [PrimaryKey, AutoIncrement] // Attributes for SQLite
    public int PlayerId { get; set; }

    [Ignore] // Ensures SQLite doesn't try to save the interface's Id property
    public object Id { get => PlayerId; set => PlayerId = (int)value; }

    public string Name { get; set; }
    public int Level { get; set; }
}
```

### 2\. System Initialization

Create an initializer `MonoBehaviour` that will run once to set up the `LocalDatabase` and register all necessary repositories.

```csharp
// GameServices.cs
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GameServices : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var serviceHost = new GameObject("[Services]");
        serviceHost.AddComponent<GameServices>();
        DontDestroyOnLoad(serviceHost);
    }

    async void Awake()
    {
        // 1. Create instances of the low-level services (only once)
        var sqlManager = new SQLiteManager();
        var jsonStore = new JsonKeyValueStore();
        
        // 2. Create and configure the LocalDatabase
        var db = new LocalDatabase();
        LocalDatabase.Instance = db;

        // 3. Create and register the repositories, injecting the dependencies
        db.RegisterRepository(new SqliteRepository<PlayerData>(sqlManager));
        db.RegisterRepository(new SqliteRepository<InventoryData>(sqlManager));
        db.RegisterRepository(new KeyValueStoreRepository<GameSettings>(jsonStore, "settings_collection"));

        // 4. Initialize all repositories centrally and asynchronously
        await db.InitializeRepositoriesAsync();

        Debug.Log("Persistence services initialized!");
    }
}
```

### 3\. Saving and Loading Data

Now, from anywhere in your game logic, you can access the data cleanly and asynchronously.

```csharp
// In a GameManager or other logic class
using Cysharp.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    private IDataRepository<PlayerData> _playerRepo;
    private PlayerData _currentPlayer;

    async void Start()
    {
        // Get the repository
        _playerRepo = LocalDatabase.Instance.GetRepository<PlayerData>();

        // Load the player with Id = 1
        _currentPlayer = await _playerRepo.GetByIdAsync(1);

        if (_currentPlayer == null)
        {
            Debug.Log("New game! Creating player...");
            _currentPlayer = new PlayerData { Name = "Hero", Level = 1 };
            await _playerRepo.InsertAsync(_currentPlayer);
        }
        else
        {
            Debug.Log($"Welcome back, {_currentPlayer.Name}!");
        }
    }

    public async void PlayerGainedLevel()
    {
        _currentPlayer.Level++;
        await _playerRepo.UpdateAsync(_currentPlayer);
        Debug.Log($"Player leveled up to level {_currentPlayer.Level}!");
    }
}
```

## Features

  * **Fully Decoupled**: The game logic doesn't know how the data is saved.
  * **Hybrid Backend Support**: Use SQLite for complex data and JSON for simple settings, all at the same time.
  * **Fully Asynchronous**: The entire system uses `UniTask` to ensure the game never freezes during I/O operations.
  * **Transactional Operations**: The `ISqlRepository` supports transactions to ensure data consistency in complex operations.
  * **Type Safety**: The use of generics ensures that you are always working with the correct data types.
  * **Centralized Access**: `LocalDatabase.Instance` provides a single and consistent entry point.

## Next Steps and Extensions

The architecture is designed to be easily extensible. Here are some possible future implementations:

  * #### **Encryption Layer (Decorator Pattern)**

    Create an `EncryptedKeyValueStore` class that implements `IKeyValueStore`. It would receive another `IKeyValueStore` in its constructor, and in its `Save`/`Load` methods, it would encrypt/decrypt the data before passing it to the inner implementation.

  * #### **Cloud Save Support**

    Create new implementations of `IKeyValueStore` for cloud services:

      * **`FirebaseKeyValueStore`**: To save data in the Firebase Realtime Database or Firestore.
      * **`PlayFabKeyValueStore`**: To integrate with PlayFab's data system.

  * #### **Unit of Work Pattern**

    For operations involving multiple repositories, the Unit of Work pattern can be implemented to ensure that both operations are saved together in a single transaction, maintaining data consistency.

  * #### **Caching Layer**

    To further optimize reads, a caching layer could be added to the `KeyValueStoreRepository` to keep recently accessed entities in memory, avoiding repetitive disk reads.
