# ObjectRepository
Generic In-Memory Object Database (Repository pattern) [![Build status](https://ci.appveyor.com/api/projects/status/5ofaya2rcml1v3nq?svg=true)](https://ci.appveyor.com/project/DiverOfDark/objectrepository)

## Why store anything in memory?

Most people would use SQL-based database for backend.
But sometimes SQL just don't fit well - i.e. when you're building a search engine or when you need to query social graph in eloquent way.

**Worst of all is when your teammate doesn't know how to write fast queries. How much time was spent debugging N+1 issues and building additional indexes just for the sake of main page load speed?**

Another approach would be to use NoSQL. Several years ago there was a big hype about it - every microservice had used MongoDB and everyone was happy getting JSON documents *(btw, how about circular references?)*

Why not store everything in-memory, sometimes flushing all on the underlying storage (i.e. file, remote database)?

Memory is cheap, and all kind of small and medium-sized projects would take no more than 1 Gb of memory. *(i.e. my favorite home project - [BudgetTracker](https://github.com/DiverOfDark/BudgetTracker), which stores daily stats of all my transcations and balances uses just 45 mb after 1.5 years of history)*

Pros:

- Access to data is easier - you don't need to think about writing queries, eager loading or ORM-dependent stuff. You work regular C# objects;
- No issues due to multithreading;
- Very fast - no network calls, no generating queries, no (de)serialization;
- You can store data in any way you like - be it XML file on disk, SQL Server, or Azure Table Storage.

Cons:

- You can't scale horizontally, thus no blue-green deployment;
- If app crashes - you can lost you latest data. *(But YOUR app never crashes, right?)*


## How it works?

In a nutshell:

- On application start connection to data storage is established, and initial load begins;
- Object model is created, (primary) indexes are calculated;
- Subscription to model's property changes (INotifyPropertyChanged) and collection changes (INotifyCollectionChanged) is created;
- When something changes - event is raised and changed object is added to queue for persisting;
- Persisting occurs by timer in background thread;
- When application exits - additional save is called.

## Usage:

```cs
// Required dependencies:
  
// Core library
Install-Package OutCode.EscapeTeams.ObjectRepository
    
// Storage - you one which you need.
Install-Package OutCode.EscapeTeams.ObjectRepository.File
Install-Package OutCode.EscapeTeams.ObjectRepository.LiteDb
Install-Package OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
    
// Optional - it is possible to store hangfire data in ObjectRepository
// Install-Package OutCode.EscapeTeams.ObjectRepository.Hangfire
```

```cs
// Data Model - it is how all will be stored.
  
public class ParentEntity : BaseEntity
{
    public ParentEntity(Guid id) => Id = id;
}
    
public class ChildEntity : BaseEntity
{
    public ChildEntity(Guid id) => Id = id;
    public Guid ParentId { get; set; }
    public string Value { get; set; }
}
```

```cs
// Object Model - something your app will work with

public class ParentModel : ModelBase
{
    public ParentModel(ParentEntity entity)
    {
        Entity = entity;
    }
    
    public ParentModel()
    {
        Entity = new ParentEntity(Guid.NewGuid());
    }
    
    // 1-Many relation
    public IEnumerable<ChildModel> Children => Multiple<ChildModel>(() => x => x.ParentId);
    
    protected override BaseEntity Entity { get; }
}
    
public class ChildModel : ModelBase
{
    private ChildEntity _childEntity;
    
    public ChildModel(ChildEntity entity)
    {
        _childEntity = entity;
    }
    
    public ChildModel() 
    {
        _childEntity = new ChildEntity(Guid.NewGuid());
    }
    
    public Guid ParentId
    {
        get => _childEntity.ParentId;
        set => UpdateProperty(_childEntity, () => x => x.ParentId, value);
    }
    
    public string Value
    {
        get => _childEntity.Value;
        set => UpdateProperty(_childEntity, () => x => x.Value, value);
    }
    
    // Indexed access
    public ParentModel Parent => Single<ParentModel>(ParentId);
    
    protected override BaseEntity Entity => _childEntity;
}
```

```cs
// ObjectRepository itself
    
public class MyObjectRepository : ObjectRepositoryBase
{
    public MyObjectRepository(IStorage storage) : base(storage, NullLogger.Instance)
    {
        IsReadOnly = true; // For testing purposes. Allows to not save changes to database.
    
        AddType((ParentEntity x) => new ParentModel(x));
        AddType((ChildEntity x) => new ChildModel(x));
    
        //// If you are using hangfire and want to store it's data in this objectrepo - uncomment this
        // this.RegisterHangfireScheme(); 
    
        Initialize();
    }
}
```

Create ObjectRepository:

```cs
var memory = new MemoryStream();
var db = new LiteDatabase(memory);
var dbStorage = new LiteDbStorage(db);
    
var repository = new MyObjectRepository(dbStorage);
await repository.WaitForInitialize();

/* if you need HangFire 
public void ConfigureServices(IServiceCollection services, ObjectRepository objectRepository)
{
    services.AddHangfire(s => s.UseHangfireStorage(objectRepository));
}
*/
```

Inserting new object:

```cs
var newParent = new ParentModel()
repository.Add(newParent);
```

After this **ParentModel** will be added to both local cache and to the queue to persist. Thus this op is O(1) and you can continue you work right away.

To check that this object is added and is the same you added:

```cs
var parents = repository.Set<ParentModel>();
var myParent = parents.Find(newParent.Id);
Assert.IsTrue(ReferenceEquals(myParent, newParent));
```

What happens here? *Set&lt;ParentModel&gt;()* returns *TableDictionary&lt;ParentModel&gt;* which is essentially *ConcurrentDictionary&lt;ParentModel, ParentModel&gt;* and provides additional methods for indexes. This allows to have a Find methods to search by Id (or other fields) without iterating all objects.

When you add something to *ObjectRepository* subscription to property changes is created, thus any property change also add object to write queue.
Property updating looks just like regular POCO object::

```cs
myParent.Children.First().Property = "Updated value";
```

You can delete object in following ways:

```cs
repository.Remove(myParent);
repository.RemoveRange(otherParents);
repository.Remove<ParentModel>(x => !x.Children.Any());
```

Deletion also happens via queue in background thread.

## How saving actually works?

When any object set tracked by *ObjectRepository* is changed (i.e. added, removed, property update) then event *ModelChanged* is raised.
*IStorage*, which is used for persistence, is subscribed to this event. All implementations of *IStorage* are enqueueing all *ModelChanged* events to 3 different queues - for addition, update, and removal.

Also each kind of *IStorage* creates timer which every 5 secs invokes actual saving.

*BTW, there exists separate API for explicit saving: **ObjectRepository.Save()**.*

Before each saving unneccessary operations are removed from the queue (i.e. multiple property changes, adding and removal of same object). After queue is sanitized actual saving is performed. 

*In all cases when object is persisted - it is persisted as a whole. So it is possible a scenario when objects are saving in different order than they were changed, including objects being saved with newer property values than were at the time of adding to queue.*

## What else?

- All libraries are targeted to .NET Standard 2.0. ObjectRepository can be used in any modern .NET app.
- All API is thread-safe. Inner collections are based on *ConcurrentDictionary* and all handlers are either have locks or don't need them. 
- Only thing you should remember - don't forget to call *ObjectRepository.Save();* when your app is going to shutdown
- If you need fast search - you can use custom indexes (works only for unique values):

```cs
repository.Set<ChildModel>().AddIndex(() => x => x.Value);
repository.Set<ChildModel>().Find(() => x => x.Value, "myValue");
```

## Who uses this?

I am using this library in all my hobby projects because it is simple and handy. In most cases I don't have to set up SQL Server or use some pricey cloud service - LiteDB/file-based approach is fine.

A while ago, when I was bootstrapping EscapeTeams startup - we used Azure Table Storage as backing storage.

## Plans for future

We want to solve major pain point of current approach - horizontal scaling. For this to happen we need to either implement distributed transactions(sic!) or to accept the fact that same objects in different instances should be not changed at the same moment of time (or latest who changed wins).

From tech point of view this can be solved in following way:

- Store event log and snapshot instead of actual model
- Find other instances (add endpoints to settings? use udp discovery? master/slave or peers?)
- Replicate eventlog between instances using any consensus algo, i.e. raft.

Other issue that exists (and worries me) is cascade deletion(and finding cases when you are deleting object that is being references by some other object). It is just not implemented, and currently exceptions may be thrown when such issue happens.
