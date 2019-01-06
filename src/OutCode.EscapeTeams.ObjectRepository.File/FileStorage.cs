using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OutCode.EscapeTeams.ObjectRepository.File
{
    public class FileStorage : IStorage, IDisposable
    {
        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _store;
        private readonly string _filename;
        private Timer _saveTimer;
        private bool _isDirty;

        public FileStorage(String filename)
        {
            _filename = filename;
            if (System.IO.File.Exists(_filename) && new FileInfo(_filename).Length > 0)
            {
                var text = System.IO.File.ReadAllText(_filename);

                var baseEntities = JsonConvert.DeserializeObject<ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>>(text,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    });

                _store = baseEntities;
            }
            else
            {
                _store = new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();
            }
        }

        public Task SaveChanges()
        {
            if (!_isDirty)
                return Task.CompletedTask;

            var contents = JsonConvert.SerializeObject(_store, Formatting.Indented,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            System.IO.File.WriteAllText(_filename, contents);
            _isDirty = false;
            
            return Task.CompletedTask;
        }

        public Task<IEnumerable<T>> GetAll<T>() =>
            Task.FromResult((IEnumerable<T>)_store.GetOrAdd(typeof(T), x => new ConcurrentList<BaseEntity>()).Cast<T>().ToList());

        public void Track(ObjectRepositoryBase objectRepository, bool isReadonly)
        {
            if (!isReadonly)
            {
                _saveTimer = new Timer(_ => SaveChanges(), null, 0, 5000);
            }

            objectRepository.ModelChanged += (change) =>
            {
                _isDirty = true;

                var itemsList = _store.GetOrAdd(change.Entity.GetType(), x => new ConcurrentList<BaseEntity>());
                
                switch (change.ChangeType)
                {
                    case ChangeType.Add:
                        itemsList.Add(change.Entity);
                        break;
                    case ChangeType.Remove:
                        itemsList.Remove(change.Entity);
                        break;
                }
            };
        }

        public event Action<Exception> OnError = delegate { };

        public void Dispose() => _saveTimer?.Dispose();
    }
}