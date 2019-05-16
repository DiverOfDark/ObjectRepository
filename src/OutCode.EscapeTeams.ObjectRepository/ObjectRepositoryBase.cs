using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public class ObjectRepositoryBase
    {
        private readonly ILogger _logger;
        private readonly List<Task> _tasks = new List<Task>();
        private bool _typeAdded;

        protected readonly ConcurrentDictionary<Type, ITableDictionary<ModelBase>> _sets = new ConcurrentDictionary<Type, ITableDictionary<ModelBase>>();
        private bool _isReadOnly;
        
        private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>();

        public event Action<ModelChangedEventArgs> ModelChanged = delegate {};

        public ObjectRepositoryBase(IStorage storage, ILogger logger)
        {
            Storage = storage;
            _logger = logger;
            Storage.OnError += RaiseOnException;
        }

        protected IStorage Storage { get; }

        protected bool ThrowIfBadItems { get; set; }

        public bool IsLoading => !_taskCompletionSource.Task.IsCompleted;

        public bool IsReadOnly
        {
            get => _isReadOnly;
            protected set
            {
                if (_typeAdded)
                    throw new NotSupportedException("Is readonly should be set before adding types!");
                _isReadOnly = value;
            }
        }

        public void AddType<TStore,TModel>(Func<TStore,TModel> converter)
            where TModel:ModelBase
        {
            if (!_typeAdded)
            {
                _typeAdded = true;
                Storage.Track(this, IsReadOnly);
            }

            _tasks.Add(AddTypeAndLoad(()=>Storage.GetAll<TStore>(), converter));
        }

        private async Task AddTypeAndLoad<TModel,TStoreEntity>(Func<Task<IEnumerable<TStoreEntity>>> sourceFunc, Func<TStoreEntity,TModel> converter) where TModel : ModelBase
        {
            var sw = new Stopwatch();
            sw.Start();

            var source = sourceFunc();

            _logger.LogInformation($"Loading entities for {typeof(TStoreEntity).Name}...");
            var value = await source;
            sw.Stop();
            
            _sets.TryAdd(typeof(TModel), value.Select(converter).Select(v =>
            {
                v.PropertyChanging += InstancePropertyChangingHandler;
                v.SetOwner(this);
                return v;
            }).ToList().ToConcurrentTable(this));

            _logger.LogInformation($"Loaded entities for {typeof(TStoreEntity).Name} in {sw.Elapsed.TotalSeconds} sec...");
        }

        public Task WaitForInitialize() => _taskCompletionSource.Task;

        protected void Initialize()
        {
            if (_tasks.Count == 0)
                throw new InvalidOperationException("No AddType was called before Initialize!");
            
            Task.WaitAll(_tasks.ToArray());

            Parallel.ForEach(_sets.Keys, key =>
            {
                try
                {
                    var sw = new Stopwatch();

                    var properties =
                        key.GetTypeInfo()
                            .GetProperties()
                            .Where(
                                v =>
                                    v.PropertyType.FullName.Contains("EscapeTeams") ||
                                    v.PropertyType.FullName.Contains("IEnumerable"))
                            .Select(
                                v => v.GetMethod.CreateDelegate(typeof(Func<,>).MakeGenericType(key, v.PropertyType)))
                            .ToList();

                    sw.Start();

                    foreach (var item in (dynamic) _sets[key])
                    {
                        foreach (var p in properties)
                        {
                            p.DynamicInvoke(item);
                        }
                    }

                    sw.Stop();
                    _logger.LogInformation(
                        $"Warming up of type {key.Name} completed, took {sw.Elapsed.TotalSeconds} seconds.");
                }
                catch (Exception)
                {
                    if (ThrowIfBadItems)
                    {
                        throw;
                    }
                }
            });
            
            _taskCompletionSource.SetResult(null);
        }

        public T Add<T>(T instance)
            where T : ModelBase
        {
            ThrowIfLoading();

            Set<T>().Add(instance);
            
            instance.PropertyChanging += InstancePropertyChangingHandler;
            ModelChanged(ModelChangedEventArgs.Added(instance));

            return instance;
        }

        private void InstancePropertyChangingHandler(ModelChangedEventArgs modelChangedEventArgs) => ModelChanged(modelChangedEventArgs);

        private void ThrowIfLoading() 
        {
            if (IsLoading)
            { 
                var total = _tasks?.Count ?? 1;
                var finished = _tasks?.Count(s => s.IsCompleted) ?? 0;
                throw new LoadingInProgressException((double)finished / total);
            }
        }

        public void Remove<T>(Func<T, bool> func)
            where T : ModelBase
        {
            ThrowIfLoading();

            var badItems = Set<T>().Where(func).ToList();
            foreach (var item in badItems)
            {
                Remove(item);
            }
        }

        public void Remove<T>(T item) 
            where T : ModelBase
        {
            ThrowIfLoading();

            Set<T>().Remove(item);
            item.PropertyChanging -= InstancePropertyChangingHandler;
            ModelChanged(ModelChangedEventArgs.Removed(item));
        }

        public void RemoveRange<T>(IEnumerable<T> item)
            where T : ModelBase
        {
            ThrowIfLoading();
            foreach (var i in item)
            {
                Remove(i);
            }
        }

        public event Action<Exception> OnException;

        protected void RaiseOnException(Exception ex) => OnException?.Invoke(ex);

        public TableDictionary<T> Set<T>() where T : ModelBase => (TableDictionary<T>) Set(typeof(T));

        public TableDictionary Set(Type t)
        {
            ThrowIfLoading();
            if (_sets.TryGetValue(t, out var result))
            {
                return result as TableDictionary;
            }

            throw new NotSupportedException("Failed to get ObjectRepository's set for type " + t?.FullName);
        }

        public async void SaveChanges()
        {
            if (IsLoading)
            {
                return;
            }

            try
            {
                await Storage.SaveChanges();
            }
            catch (Exception ex)
            {
                RaiseOnException(ex);
            }
        }
    }
}