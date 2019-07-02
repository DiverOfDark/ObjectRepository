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
Required dependencies:
  
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
    public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
    
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
        set => UpdateProperty(() => _childEntity.ParentId, value);
    }
    
    public string Value
    {
        get => _childEntity.Value;
        set => UpdateProperty(() => _childEntity.Value, value);
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
        IsReadOnly = true; // Для тестов, позволяет не сохранять изменения в базу
    
        AddType((ParentEntity x) => new ParentModel(x));
        AddType((ChildEntity x) => new ChildModel(x));
    
        // Если используется Hangfire и необходимо хранить модель данных для Hangfire в ObjectRepository
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

What happens here? *Set<ParentModel>()* returns *TableDictionary<ParentModel>* which is essentially ConcurrentDictionary<ParentModel, ParentModel>* and provides additional methods for indexes. This allows to have a Find methods to search by Id (or other fields) without iterating all objects.

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

## Как работает сохранение?

*ObjectRepository* при изменении отслеживаемых объектов (как добавление или удаление, так и изменение свойств) вызывает событие *ModelChanged*, на которое подписан *IStorage*. Реализации *IStorage* при возникновении события *ModelChanged* складывают изменения в 3 очереди - на добавление, на обновление, и на удаление.

Также реализации *IStorage* при инициализации создают таймер, который каждые 5 секунд вызывает сохранение изменений. 

*Кроме того существует API для принудительного вызова сохранения: **ObjectRepository.Save()**.*

Перед каждым сохранением сначала происходит удаление из очередей бессмысленных операций (например дубликаты событий - когда объект менялся дважды или быстрое добавление/удаление объектов), и только потом само сохранение. 

*Во всех случаях сохраняется актуальный объект целиком, поэтому возможна ситуация, когда объекты сохраняются в другом порядке, чем менялись, в том числе могут сохраняться более новые версии объектов, чем на момент добавления в очередь.*

## Что есть ещё?

- Все библиотеки основаны на .NET Standard 2.0. Можно использовать в любом современном .NET проекте.
- API потокобезопасен. Внутренние коллекции реализованы на базе *ConcurrentDictionary*, обработчики событий имеют либо блокировки, либо не нуждаются в них. 
Единственное о чем стоит помнить - при завершении приложения вызвать *ObjectRepository.Save();*
- Произвольные индексы (требуют уникальность):

```cs
repository.Set<ChildModel>().AddIndex(x => x.Value);
repository.Set<ChildModel>().Find(x => x.Value, "myValue");
```

## Кто это использует?

Лично я начал использовать этот подход во всех хобби-проектах, потому что это удобно, и не требует больших затрат на написание слоя доступа к данным или разворачивания тяжелой инфраструктуры. Лично мне, как правило, достаточно хранения данных в litedb или в файле. 

Но в прошлом, когда с командой делали ныне почивший стартап EscapeTeams (*думал вот они, деньги - ан нет, опять опыт*) - использовали для хранения данных Azure Table Storage.

## Планы на будущее

Хочется починить один из основных минусов данного подхода - горизонтальное масштабирование. Для этого нужны либо распределенные транзакции (sic!), либо принять волевое решение, что одни и те же данные из разных инстансов меняться не должны, либо пускай меняются по принципу "кто последний - тот и прав".

С технической точки зрения я вижу возможной следующую схему:

- Хранить вместо объектной модели EventLog и Snapshot
- Находить другие инстансы (добавлять в настройки конечные точки всех инстансов? udp discovery? master/slave?)
- Реплицировать между инстансами EventLog через любой из алгоритмов консенсуса, например RAFT.

Так же существует ещё одна проблема, которая меня беспокоит - это каскадное удаление, либо обнаружение случаев удаления объектов, на которые есть ссылки из других объектов. 

## Исходный код
Если вы дочитали до сюда - то дальше остается читать только код, его можно 
[найти на GitHub](https://github.com/DiverOfDark/ObjectRepository).
