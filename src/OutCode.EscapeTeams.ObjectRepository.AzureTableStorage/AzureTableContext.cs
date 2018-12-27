using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
{
    /// <summary>
    /// Data Context bound to Azure Table Storage.
    /// </summary>
    public class AzureTableContext : IStorage, IDisposable
    {
        private readonly CloudTableClient _client;
        private Timer _saveTimer;

        private readonly CloudTable _migrationTables;
        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToAdd;
        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToRemove;
        private readonly ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> _entitiesToUpdate;

        private readonly ConcurrentDictionary<BaseEntity, TableEntity> _adapters;
        
        private int _saveInProgress;

        public AzureTableContext(CloudTableClient client)
        {
            _client = client;
            _migrationTables = _client.GetTableReference("MigrationTable");

            _entitiesToAdd = new ConcurrentDictionary<Type,    ConcurrentList<BaseEntity>>();
            _entitiesToRemove = new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();
            _entitiesToUpdate = new ConcurrentDictionary<Type, ConcurrentList<BaseEntity>>();
            _adapters = new ConcurrentDictionary<BaseEntity, TableEntity>();
        }

        public event Action<Exception> OnError = delegate {};

        public string ExportStream()
        {
            var result = new
            {
                add = _entitiesToAdd.ToDictionary(v => v.Key, v => v.Value.ToList()),
                remove = _entitiesToRemove.ToDictionary(v => v.Key, v => v.Value.ToList()),
                mod = _entitiesToUpdate.ToDictionary(v => v.Key, v => v.Value.ToList()),
            };

            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Saves all data changes to the tables.
        /// </summary>
        public async Task SaveChanges()
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

                    await ProcessAction(_entitiesToAdd, (batch, entity) => batch.Insert(CreateAdapter(entity)));
                    await ProcessAction(_entitiesToUpdate, (batch, entity) => batch.Replace(CreateAdapter(entity)));
                    await ProcessAction(_entitiesToRemove, (batch, entity) => batch.Delete(CreateAdapter(entity)));
                }
                finally
                {
                    _saveInProgress = 0;
                }
            }
        }

        private ITableEntity CreateAdapter(BaseEntity entity)
        {
            return _adapters.GetOrAdd(entity, be => new DateTimeAwareTableEntityAdapter<BaseEntity>(be));
        }

        /// <summary>
        /// Returns the entire contents of a table.
        /// </summary>
        public Task<IEnumerable<T>> GetAll<T>()
        {
            var task = GetType().GetMethod(nameof(ExecuteQuery), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(this, new object[0]);
            return (Task<IEnumerable<T>>) task;
        }

        private async Task<IEnumerable<T>> ExecuteQuery<T>() where T:BaseEntity, new()
        {
            var tableReference = _client.GetTableReference(typeof(T).Name);
            await tableReference.CreateIfNotExistsAsync();

            var result = await tableReference.ExecuteQueryAsync(new TableQuery<DateTimeAwareTableEntityAdapter<T>>());

            foreach (var item in result)
            {
                _adapters.TryAdd(item.OriginalEntity, item);
            }
            
            return result.Select(v=>v.OriginalEntity).ToList();
        }
 
        public async Task ApplyMigration(AzureTableMigration migration)
        {
            await _migrationTables.CreateIfNotExistsAsync();
            var appliedMigrations = await _migrationTables.ExecuteQueryAsync(new TableQuery<TableEntity>());

            if (appliedMigrations.Any(s => s.PartitionKey == "migration" && s.RowKey == migration.Name))
            {
                return;
            }

            await migration.Execute(this);
            var entity = new TableEntity {PartitionKey = "migration", RowKey = migration.Name};
            await _migrationTables.ExecuteAsync(TableOperation.Insert(entity));
            await SaveChanges();
        }

        /// <summary>
        /// Registers an entity for one of the operations.
        /// </summary>
        private void AddEntityToLookup<T>(T entity, ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> lookup)
            where T: BaseEntity
        {
            var set = lookup.GetOrAdd(entity.GetType(), type => new ConcurrentList<BaseEntity>());
            set.Add(entity);
        }

        /// <summary>
        /// Performs an action on the group of objects.
        /// </summary>
        private async Task ProcessAction(ConcurrentDictionary<Type, ConcurrentList<BaseEntity>> lookup, Action<TableBatchOperation, BaseEntity> entityAction)
        {
            const int BATCH_SIZE = 100; // enforced by Azure Table Storage

            foreach (var type in lookup.Keys)
            {
                var batches = SplitToBatches(lookup[type], BATCH_SIZE).ToList();
                foreach(var batch in batches)
                    try
                    {
                        await ProcessObjectList(lookup[type], type, batch, entityAction);

                        if (lookup == _entitiesToRemove)
                        {
                            foreach (var b in batch)
                            {
                                _entitiesToRemove[type].Remove(b);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError(ex);
                    }
            }
        }

        /// <summary>
        /// Processes a single action on the objects.
        /// </summary>
        private async Task ProcessObjectList<T>(ConcurrentList<T> concurrentQueue, Type type, IEnumerable<T> list, Action<TableBatchOperation, T> entityAction)
            where T: BaseEntity
        {
            var tableName = type.Name;
            var tableRef = _client.GetTableReference(tableName);
            var exs = new List<Exception>();
            list = list.ToList();
            try
            {
                var batch = new TableBatchOperation();
                foreach (var obj in list)
                    entityAction(batch, obj);

                await tableRef.ExecuteBatchAsync(batch);
            }
            catch 
            {
                foreach (var item in list)
                {
                    try
                    {
                        var batch = new TableBatchOperation();
                        entityAction(batch, item);
                        await tableRef.ExecuteBatchAsync(batch);
                    }
                    catch(Exception exception)
                    {
                        concurrentQueue.Add(item);

                        var data = item.GetPropertiesAsRawData();

                        exs.Add(new Exception($"Failed to update {item.Id}\r\n{data}", exception));
                    }
                }

                if (exs.Any())
                {
                    throw new AggregateException("Azure batch failed", exs);
                }
            }
        }

        /// <summary>
        /// Splits a sequence into a list of subsequences of given length.
        /// </summary>
        private static IEnumerable<IEnumerable<T>> SplitToBatches<T>(ConcurrentList<T> sequence, int batchSize)
        {
            var batch = new List<T>(batchSize);

            T curr;
            while(sequence.TryTake(out curr))
            {
                if (batch.Count < batchSize)
                {
                    if (!batch.Contains(curr))
                        batch.Add(curr);
                }
                else
                {
                    yield return batch;
                    batch = new List<T>(batchSize) {curr};
                }
            }

            if(batch.Count > 0)
                yield return batch;
        }

        public Dictionary<Type, int> GetStatistics()
        {
            var d = new Dictionary<Type, int>();
            foreach (var item in _entitiesToAdd)
            {
                d[item.Key] = item.Value.Count();
            }

            foreach (var item in _entitiesToRemove)
            {
                if (!d.ContainsKey(item.Key))
                    d[item.Key] = 0;
                d[item.Key] += item.Value.Count();
            }
            
            foreach (var item in _entitiesToUpdate)
            {
                if (!d.ContainsKey(item.Key))
                    d[item.Key] = 0;
                d[item.Key] += item.Value.Count();
            }

            return d;
        }

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
                        AddEntityToLookup((BaseEntity)change.Entity, _entitiesToAdd);
                        break;
                    case ChangeType.Remove:
                        AddEntityToLookup((BaseEntity)change.Entity, _entitiesToRemove);
                        break;
                }
            };

        }

        public void Dispose() => _saveTimer?.Dispose();
    }
}
