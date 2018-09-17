using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace OutCode.EscapeTeams.ObjectRepository.LiteDB
{
    public class LiteDbStorage : IStorage, IDisposable
    {
        private readonly LiteDatabase _database;
        private Timer _saveTimer;
        private int _saveInProgress;

        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToAdd =
            new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();

        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToRemove =
            new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();

        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToUpdate =
            new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();

        private readonly BsonMapper _mapper;

        public LiteDbStorage(LiteDatabase database)
        {
            _database = database;
            _mapper = new BsonMapper();
        }

        public Task SaveChanges()
        {
            if (Interlocked.CompareExchange(ref _saveInProgress, 1, 0) == 1)
            {
                try
                {
                    foreach (var remove in _entitiesToRemove.ToList())
                    {
                        foreach (var removeItem in remove.Value.ToList())
                        {
                            if (_entitiesToAdd.TryGetValue(remove.Key, out var toAddList))
                            {
                                if (toAddList?.Contains(removeItem) == true)
                                {
                                    remove.Value.Remove(removeItem);
                                    toAddList.Remove(removeItem);
                                }
                            }

                            if (_entitiesToUpdate.TryGetValue(remove.Key, out var removeList))
                            {
                                removeList.Remove(removeItem);
                            }
                        }
                    }

                    ProcessAction(_entitiesToAdd, (x, y) => y.Insert(_mapper.ToDocument(x)));
                    ProcessAction(_entitiesToUpdate, (x, y) => y.Update(_mapper.ToDocument(x)));
                    ProcessAction(_entitiesToRemove, (x, y) => y.Delete(_mapper.ToDocument(x)));

                    _database.Shrink();
                }
                finally
                {
                    _saveInProgress = 0;
                }
            }

            return Task.CompletedTask;
        }

        private void ProcessAction(ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> dictionary,
            Action<BaseEntity, LiteCollection<BsonDocument>> entity)
        {
            foreach (var addPair in dictionary)
            {
                var collection = _database.GetCollection(addPair.Key.Name);
                var itemsToAction = addPair.Value.ToList();
                foreach (var item in itemsToAction)
                {
                    try
                    {
                        addPair.Value.Remove(item);
                        entity(item, collection);
                    }
                    catch (Exception ex)
                    {
                        OnError(ex);
                        addPair.Value.Add(item);
                    }
                }
            }
        }

        public Task<IEnumerable<T>> GetAll<T>() =>
            Task.FromResult((IEnumerable<T>) _database.GetCollection<T>().FindAll().ToList());

        public void Track(ObjectRepositoryBase objectRepository, bool isReadonly)
        {
            if (!isReadonly)
            {
                _saveTimer = new Timer(_ => SaveChanges(), null, 0, 5000);
            }

            objectRepository.ModelChanged += (change) =>
            {
                switch (change.ChangeType)
                {
                    case ChangeType.Update:
                        AddEntityToLookup((BaseEntity) change.Entity, _entitiesToUpdate);
                        break;
                    case ChangeType.Add:
                        AddEntityToLookup((BaseEntity) change.Entity, _entitiesToAdd);
                        break;
                    case ChangeType.Remove:
                        AddEntityToLookup((BaseEntity) change.Entity, _entitiesToRemove);
                        break;
                }
            };
        }

        public event Action<Exception> OnError = delegate { };

        public void Dispose() => _saveTimer?.Dispose();

        /// <summary>
        /// Registers an entity for one of the operations.
        /// </summary>
        private void AddEntityToLookup<T>(T entity, ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> lookup)
            where T : BaseEntity
        {
            var set = lookup.GetOrAdd(entity.GetType(), type => new ConcurrentList<BaseEntity>());
            set.Add(entity);
        }
    }
}